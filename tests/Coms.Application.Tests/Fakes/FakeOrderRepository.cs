using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Orders;
using Coms.Domain.Products;

namespace Coms.Application.Tests.Fakes
{
    /// <summary>
    /// In-memory orders. Mirrors what the SQL repository and the
    /// usp_Order_Fulfil procedure do, including stock movement against the
    /// product fake, so services can be tested end to end without a database.
    /// </summary>
    internal sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly FakeProductRepository _products;
        private readonly FakeCustomerRepository _customers;

        public FakeOrderRepository(FakeProductRepository products, FakeCustomerRepository customers)
        {
            _products = products;
            _customers = customers;
        }

        public List<Order> Orders { get; } = new List<Order>();

        public Order Add(Order order)
        {
            order.Id = Orders.Count == 0 ? 1 : Orders.Max(o => o.Id) + 1;
            order.OrderNumber = "ORD-" + order.OrderDate.Year + "-" + order.Id.ToString("000000");
            order.RowVersion = Versions.Next();
            foreach (OrderLine line in order.Lines)
            {
                line.OrderId = order.Id;
            }

            if (order.History.Count == 0)
            {
                order.History.Add(new OrderStatusChange { OrderId = order.Id, FromStatus = null, ToStatus = order.Status, ChangedAtUtc = DateTime.UtcNow, ChangedBy = "seed" });
            }

            Orders.Add(order);
            return order;
        }

        public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));
        }

        public Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Orders.FirstOrDefault(o => o.OrderNumber == orderNumber));
        }

        public Task<PagedResult<OrderSummary>> SearchAsync(OrderFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            IEnumerable<Order> query = Orders;
            if (filter.Status.HasValue)
            {
                query = query.Where(o => o.Status == filter.Status.Value);
            }

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == filter.CustomerId.Value);
            }

            List<OrderSummary> all = query.OrderByDescending(o => o.OrderDate).Select(ToSummary).ToList();
            List<OrderSummary> page = all.Skip(paging.Offset).Take(paging.PageSize).ToList();
            return Task.FromResult(new PagedResult<OrderSummary>(page, all.Count, paging.Page, paging.PageSize));
        }

        public Task<IReadOnlyList<OrderSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<OrderSummary> rows = Orders.OrderByDescending(o => o.Id).Take(count).Select(ToSummary).ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<OrderStatusChange>> GetHistoryAsync(int orderId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<OrderStatusChange> rows = Orders.Where(o => o.Id == orderId).SelectMany(o => o.History).ToList();
            return Task.FromResult(rows);
        }

        public Task<PagedResult<OrderStatusChange>> GetRecentHistoryAsync(PagedRequest paging, CancellationToken cancellationToken = default)
        {
            List<OrderStatusChange> all = Orders.SelectMany(o => o.History).OrderByDescending(h => h.ChangedAtUtc).ToList();
            List<OrderStatusChange> page = all.Skip(paging.Offset).Take(paging.PageSize).ToList();
            return Task.FromResult(new PagedResult<OrderStatusChange>(page, all.Count, paging.Page, paging.PageSize));
        }

        public Task InsertAsync(Order order, string userName, CancellationToken cancellationToken = default)
        {
            order.History.Clear();
            Add(order);
            order.CreatedBy = order.UpdatedBy = userName;
            order.History[0].ChangedBy = userName;
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Order order, string userName, CancellationToken cancellationToken = default)
        {
            Order? stored = Orders.FirstOrDefault(o => o.Id == order.Id);
            if (stored == null || !Versions.Match(stored.RowVersion, order.RowVersion))
            {
                return Task.FromResult(false);
            }

            if (!ReferenceEquals(stored, order))
            {
                Orders.Remove(stored);
                Orders.Add(order);
            }

            foreach (OrderLine line in order.Lines)
            {
                line.OrderId = order.Id;
            }

            order.RowVersion = Versions.Next();
            order.UpdatedBy = userName;
            return Task.FromResult(true);
        }

        public Task<bool> ChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, byte[] rowVersion, string userName, string? comment, CancellationToken cancellationToken = default)
        {
            Order? order = Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null || order.Status != from || !Versions.Match(order.RowVersion, rowVersion))
            {
                return Task.FromResult(false);
            }

            order.Status = to;
            order.RowVersion = Versions.Next();
            order.UpdatedBy = userName;
            order.History.Add(new OrderStatusChange { OrderId = orderId, FromStatus = from, ToStatus = to, ChangedAtUtc = DateTime.UtcNow, ChangedBy = userName, Comment = comment });
            return Task.FromResult(true);
        }

        public Task<Result> FulfilAsync(int orderId, byte[] rowVersion, string userName, string? comment, bool allowNegativeStock, CancellationToken cancellationToken = default)
        {
            Order? order = Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                return Task.FromResult(Result.Failure(ErrorCode.NotFound, "Order not found."));
            }

            if (!Versions.Match(order.RowVersion, rowVersion))
            {
                return Task.FromResult(Result.Failure(ErrorCode.Conflict, "The order was modified by another user."));
            }

            if (order.Status != OrderStatus.Approved)
            {
                return Task.FromResult(Result.Failure(ErrorCode.InvalidStatus, "Order cannot be fulfilled from status '" + order.Status + "'."));
            }

            if (order.Lines.Count == 0)
            {
                return Task.FromResult(Result.Failure(ErrorCode.NoLines, "Order has no lines."));
            }

            var demand = order.Lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            if (!allowNegativeStock)
            {
                foreach (KeyValuePair<int, decimal> pair in demand)
                {
                    Product product = _products.Products.First(p => p.Id == pair.Key);
                    if (product.QuantityOnHand < pair.Value)
                    {
                        return Task.FromResult(Result.Failure(ErrorCode.InsufficientStock, "Insufficient stock: " + product.Sku));
                    }
                }
            }

            foreach (KeyValuePair<int, decimal> pair in demand)
            {
                _products.Products.First(p => p.Id == pair.Key).QuantityOnHand -= pair.Value;
            }

            order.Status = OrderStatus.Fulfilled;
            order.RowVersion = Versions.Next();
            order.History.Add(new OrderStatusChange { OrderId = orderId, FromStatus = OrderStatus.Approved, ToStatus = OrderStatus.Fulfilled, ChangedAtUtc = DateTime.UtcNow, ChangedBy = userName, Comment = comment });
            return Task.FromResult(Result.Success());
        }

        private OrderSummary ToSummary(Order o)
        {
            var customer = _customers.Customers.FirstOrDefault(c => c.Id == o.CustomerId);
            return new OrderSummary
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                OrderDate = o.OrderDate,
                RequiredDate = o.RequiredDate,
                CustomerReference = o.CustomerReference,
                CustomerId = o.CustomerId,
                CustomerNumber = customer?.CustomerNumber ?? string.Empty,
                CustomerName = customer?.Name ?? string.Empty,
                Subtotal = o.Subtotal,
                TaxRate = o.TaxRate,
                TaxAmount = o.TaxAmount,
                Total = o.Total,
                LineCount = o.Lines.Count,
                UpdatedAtUtc = o.UpdatedAtUtc,
                UpdatedBy = o.UpdatedBy
            };
        }
    }
}
