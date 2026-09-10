using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Coms.Application.Orders
{
    public sealed class OrderService : IOrderService
    {
        private readonly IOrderRepository _orders;
        private readonly ICustomerRepository _customers;
        private readonly IProductRepository _products;
        private readonly ICurrentUser _currentUser;
        private readonly ComsOptions _options;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IOrderRepository orders,
            ICustomerRepository customers,
            IProductRepository products,
            ICurrentUser currentUser,
            ComsOptions options,
            ILogger<OrderService> logger)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Order?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            return _orders.GetByIdAsync(id, cancellationToken);
        }

        public Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
        {
            return _orders.GetByNumberAsync(orderNumber, cancellationToken);
        }

        public Task<PagedResult<OrderSummary>> SearchAsync(OrderFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return _orders.SearchAsync(filter, paging, cancellationToken);
        }

        public Task<IReadOnlyList<OrderSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return _orders.GetRecentAsync(count, cancellationToken);
        }

        public Task<PagedResult<OrderStatusChange>> GetRecentHistoryAsync(HistoryFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return _orders.GetRecentHistoryAsync(filter, paging, cancellationToken);
        }

        public async Task<Result<Order>> CreateDraftAsync(OrderInput input, CancellationToken cancellationToken = default)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Result<Order> built = await BuildAsync(input, existing: null, cancellationToken).ConfigureAwait(false);
            if (built.IsFailure)
            {
                return built;
            }

            Order order = built.Value;
            order.Status = OrderStatus.Draft;
            await _orders.InsertAsync(order, _currentUser.UserName, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Order {OrderNumber} created by {User}", order.OrderNumber, _currentUser.UserName);
            return Result<Order>.Success(order);
        }

        public async Task<Result<Order>> UpdateAsync(int orderId, byte[] rowVersion, OrderInput input, CancellationToken cancellationToken = default)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Order? existing = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                return Result<Order>.NotFound("Order");
            }

            if (!RowVersions.Match(existing.RowVersion, rowVersion))
            {
                return Result<Order>.Conflict();
            }

            if (!existing.CanEdit)
            {
                return Result<Order>.Failure(ErrorCode.InvalidStatus, "Order " + existing.OrderNumber + " is " + existing.Status + " and can no longer be edited.");
            }

            Result<Order> built = await BuildAsync(input, existing, cancellationToken).ConfigureAwait(false);
            if (built.IsFailure)
            {
                return built;
            }

            Order order = built.Value;
            bool updated = await _orders.UpdateAsync(order, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            if (!updated)
            {
                return Result<Order>.Conflict();
            }

            return Result<Order>.Success(order);
        }

        public Task<Result<Order>> PreviewAsync(OrderInput input, CancellationToken cancellationToken = default)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return BuildAsync(input, existing: null, cancellationToken);
        }

        public async Task<Result<Order>> SubmitAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default)
        {
            Result<Order> loaded = await LoadForTransitionAsync(orderId, rowVersion, OrderStatus.Submitted, cancellationToken).ConfigureAwait(false);
            if (loaded.IsFailure)
            {
                return loaded;
            }

            Order order = loaded.Value;

            if (order.Lines.Count == 0)
            {
                return Result<Order>.Failure(ErrorCode.NoLines, "Order " + order.OrderNumber + " has no lines and cannot be submitted.");
            }

            Result customerCheck = await CheckCustomerCanOrderAsync(order.CustomerId, cancellationToken).ConfigureAwait(false);
            if (customerCheck.IsFailure)
            {
                return Result<Order>.From(customerCheck);
            }

            foreach (OrderLine line in order.Lines)
            {
                Product? product = await _products.GetByIdAsync(line.ProductId, cancellationToken).ConfigureAwait(false);
                if (product == null)
                {
                    return Result<Order>.Failure(ErrorCode.NotFound, "Product for line " + line.LineNumber + " (" + line.Sku + ") no longer exists.");
                }

                if (!product.IsActive)
                {
                    return Result<Order>.Failure(ErrorCode.ProductInactive, "Product " + product.Sku + " on line " + line.LineNumber + " is inactive.");
                }
            }

            return await ChangeStatusAsync(order, OrderStatus.Submitted, comment, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<Order>> ApproveAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default)
        {
            Result<Order> loaded = await LoadForTransitionAsync(orderId, rowVersion, OrderStatus.Approved, cancellationToken).ConfigureAwait(false);
            if (loaded.IsFailure)
            {
                return loaded;
            }

            Order order = loaded.Value;

            Result customerCheck = await CheckCustomerCanOrderAsync(order.CustomerId, cancellationToken).ConfigureAwait(false);
            if (customerCheck.IsFailure)
            {
                return Result<Order>.From(customerCheck);
            }

            CustomerBalance? balance = await _customers.GetBalanceAsync(order.CustomerId, cancellationToken).ConfigureAwait(false);
            if (balance != null && balance.WouldExceedLimit(order.Total))
            {
                return Result<Order>.Failure(
                    ErrorCode.CreditLimitExceeded,
                    "Approving " + order.OrderNumber + " (" + Amount(order.Total) + ") would exceed the credit limit. " +
                    "Limit " + Amount(balance.CreditLimit ?? 0) + ", outstanding " + Amount(balance.OutstandingBalance) +
                    ", committed " + Amount(balance.CommittedTotal) + ", available " + Amount(balance.CreditAvailable ?? 0) + ".");
            }

            return await ChangeStatusAsync(order, OrderStatus.Approved, comment, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<Order>> FulfilAsync(int orderId, byte[] rowVersion, string? comment, CancellationToken cancellationToken = default)
        {
            Result<Order> loaded = await LoadForTransitionAsync(orderId, rowVersion, OrderStatus.Fulfilled, cancellationToken).ConfigureAwait(false);
            if (loaded.IsFailure)
            {
                return loaded;
            }

            Order order = loaded.Value;

            Result result = await _orders.FulfilAsync(order.Id, order.RowVersion, _currentUser.UserName, comment, _options.AllowNegativeStock, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Result<Order>.From(result);
            }

            _logger.LogInformation("Order {OrderNumber} fulfilled by {User}", order.OrderNumber, _currentUser.UserName);
            return await ReloadAsync(order.Id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<Order>> CancelAsync(int orderId, byte[] rowVersion, string? reason, CancellationToken cancellationToken = default)
        {
            Result<Order> loaded = await LoadForTransitionAsync(orderId, rowVersion, OrderStatus.Cancelled, cancellationToken).ConfigureAwait(false);
            if (loaded.IsFailure)
            {
                return loaded;
            }

            return await ChangeStatusAsync(loaded.Value, OrderStatus.Cancelled, reason, cancellationToken).ConfigureAwait(false);
        }

        /* ------------------------------------------------------------------ */

        private async Task<Result<Order>> BuildAsync(OrderInput input, Order? existing, CancellationToken cancellationToken)
        {
            var errors = new List<ValidationError>();

            Customer? customer = await _customers.GetByIdAsync(input.CustomerId, cancellationToken).ConfigureAwait(false);
            if (customer == null)
            {
                errors.Add(new ValidationError(nameof(OrderInput.CustomerId), "Customer was not found."));
            }
            else if (!customer.CanOrder)
            {
                errors.Add(new ValidationError(nameof(OrderInput.CustomerId), "Customer " + customer.CustomerNumber + " is " + customer.Status + " and cannot place orders."));
            }

            Order order = existing ?? new Order();
            order.CustomerId = input.CustomerId;
            order.OrderDate = input.OrderDate.Date;
            order.RequiredDate = input.RequiredDate?.Date;
            order.CustomerReference = input.CustomerReference;
            order.Notes = input.Notes;
            order.TaxRate = existing?.TaxRate ?? _options.TaxRate;
            order.ShipTo = input.ShipTo != null && !input.ShipTo.IsEmpty
                ? input.ShipTo.Copy()
                : customer?.EffectiveShippingAddress.Copy();

            var lines = new List<OrderLine>();
            for (int i = 0; i < input.Lines.Count; i++)
            {
                OrderLineInput lineInput = input.Lines[i];
                string prefix = "Lines[" + i + "].";

                Product? product = await _products.GetByIdAsync(lineInput.ProductId, cancellationToken).ConfigureAwait(false);
                if (product == null)
                {
                    errors.Add(new ValidationError(prefix + nameof(OrderLineInput.ProductId), "Product was not found."));
                    continue;
                }

                if (!product.IsActive)
                {
                    errors.Add(new ValidationError(prefix + nameof(OrderLineInput.ProductId), "Product " + product.Sku + " is inactive."));
                }

                lines.Add(new OrderLine
                {
                    ProductId = product.Id,
                    Sku = product.Sku,
                    Description = string.IsNullOrWhiteSpace(lineInput.Description) ? product.Name : lineInput.Description!.Trim(),
                    Quantity = lineInput.Quantity,
                    UnitPrice = lineInput.UnitPrice ?? product.UnitPrice,
                    UnitCost = product.CostPrice,
                    DiscountPercent = lineInput.DiscountPercent
                });
            }

            order.Lines = lines;
            order.Normalize();
            order.Recalculate();
            errors.AddRange(order.Validate());

            return errors.Count == 0 ? Result<Order>.Success(order) : Result<Order>.Invalid(errors);
        }

        private async Task<Result<Order>> LoadForTransitionAsync(int orderId, byte[] rowVersion, OrderStatus target, CancellationToken cancellationToken)
        {
            Order? order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order == null)
            {
                return Result<Order>.NotFound("Order");
            }

            if (!RowVersions.Match(order.RowVersion, rowVersion))
            {
                return Result<Order>.Conflict();
            }

            if (!order.CanTransitionTo(target))
            {
                return Result<Order>.Failure(
                    ErrorCode.InvalidStatus,
                    "Order " + order.OrderNumber + " is " + order.Status + " and cannot be moved to " + target + ".");
            }

            return Result<Order>.Success(order);
        }

        private async Task<Result> CheckCustomerCanOrderAsync(int customerId, CancellationToken cancellationToken)
        {
            Customer? customer = await _customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
            if (customer == null)
            {
                return Result.NotFound("Customer");
            }

            if (!customer.CanOrder)
            {
                return Result.Failure(ErrorCode.CustomerNotActive, "Customer " + customer.CustomerNumber + " is " + customer.Status + ".");
            }

            return Result.Success();
        }

        private async Task<Result<Order>> ChangeStatusAsync(Order order, OrderStatus target, string? comment, CancellationToken cancellationToken)
        {
            OrderStatus from = order.Status;
            bool changed = await _orders.ChangeStatusAsync(order.Id, from, target, order.RowVersion, _currentUser.UserName, TrimToNull(comment), cancellationToken).ConfigureAwait(false);
            if (!changed)
            {
                return Result<Order>.Conflict();
            }

            _logger.LogInformation("Order {OrderNumber} {From} -> {To} by {User}", order.OrderNumber, from, target, _currentUser.UserName);
            return await ReloadAsync(order.Id, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result<Order>> ReloadAsync(int orderId, CancellationToken cancellationToken)
        {
            Order? order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            return order == null ? Result<Order>.NotFound("Order") : Result<Order>.Success(order);
        }

        private static string Amount(decimal value)
        {
            return value.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string? TrimToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
        }
    }
}
