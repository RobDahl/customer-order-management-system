using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Notes;
using Coms.Application.Orders;
using Coms.Application.Products;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.CanView)]
    public class OrdersController : ComsController
    {
        private const string SaveAction = "save";
        private const string AddLineAction = "addLine";
        private const string RemoveLinePrefix = "removeLine-";

        private readonly IOrderService _orders;
        private readonly ICustomerService _customers;
        private readonly IProductService _products;
        private readonly IInvoiceService _invoices;
        private readonly INoteService _notes;

        public OrdersController(IOrderService orders, ICustomerService customers, IProductService products, IInvoiceService invoices, INoteService notes)
        {
            _orders = orders;
            _customers = customers;
            _products = products;
            _invoices = invoices;
            _notes = notes;
        }

        [HttpGet]
        public async Task<IActionResult> Index(OrderListModel query)
        {
            if (query.CustomerId.HasValue)
            {
                Customer? customer = await _customers.GetAsync(query.CustomerId.Value);
                query.CustomerName = customer == null ? null : customer.CustomerNumber + " " + customer.Name;
            }

            query.Results = await _orders.SearchAsync(query.ToFilter(), query.ToPagedRequest());
            return View(query);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            Order? order = await _orders.GetAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            var model = new OrderDetailsModel
            {
                Order = order,
                Customer = await _customers.GetAsync(order.CustomerId),
                Invoice = await _invoices.GetLiveForOrderAsync(id),
                Notes = await _notes.GetAsync(NoteEntityType.Order, id),
                AllowedTransitions = OrderStatusMachine.AllowedTransitions(order.Status)
            };

            return View(model);
        }

        /* ---------------- create / edit ---------------- */

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Create(int? customerId)
        {
            var model = new OrderFormModel();
            model.Input.CustomerId = customerId ?? 0;
            model.Input.Lines.Add(new OrderLineInput { Quantity = 1 });
            await PopulateAsync(model);
            return View("Form", model);
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderFormModel model, string action)
        {
            if (await HandleLineActionAsync(model, action))
            {
                return View("Form", model);
            }

            Result<Order> result = await _orders.CreateDraftAsync(model.Input);
            if (result.IsFailure)
            {
                AddErrors(result, nameof(OrderFormModel.Input));
                await PopulateAsync(model);
                return View("Form", model);
            }

            Flash("Order " + result.Value.OrderNumber + " created as a draft.");
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Edit(int id)
        {
            Order? order = await _orders.GetAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            if (!order.CanEdit)
            {
                FlashError("Order " + order.OrderNumber + " is " + order.Status + " and can no longer be edited.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new OrderFormModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                RowVersion = Convert.ToBase64String(order.RowVersion),
                Input = ToInput(order),
                Preview = order
            };

            await PopulateAsync(model);
            return View("Form", model);
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrderFormModel model, string action)
        {
            model.Id = id;

            if (await HandleLineActionAsync(model, action))
            {
                return View("Form", model);
            }

            Result<Order> result = await _orders.UpdateAsync(id, ParseRowVersion(model.RowVersion), model.Input);
            if (result.IsFailure)
            {
                AddErrors(result, nameof(OrderFormModel.Input));
                await PopulateAsync(model);
                return View("Form", model);
            }

            Flash("Order " + result.Value.OrderNumber + " saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        /* ---------------- transitions ---------------- */

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Transition(int id, OrderStatus to)
        {
            Order? order = await _orders.GetAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            if (!order.CanTransitionTo(to))
            {
                FlashError("Order " + order.OrderNumber + " is " + order.Status + " and cannot be moved to " + to + ".");
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(new TransitionModel { Id = id, To = to, RowVersion = Convert.ToBase64String(order.RowVersion), Order = order });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transition(TransitionModel model)
        {
            byte[] rowVersion = ParseRowVersion(model.RowVersion);
            Result<Order> result;

            switch (model.To)
            {
                case OrderStatus.Submitted:
                    result = await _orders.SubmitAsync(model.Id, rowVersion, model.Comment);
                    break;
                case OrderStatus.Approved:
                    result = await _orders.ApproveAsync(model.Id, rowVersion, model.Comment);
                    break;
                case OrderStatus.Fulfilled:
                    result = await _orders.FulfilAsync(model.Id, rowVersion, model.Comment);
                    break;
                case OrderStatus.Cancelled:
                    result = await _orders.CancelAsync(model.Id, rowVersion, model.Comment);
                    break;
                default:
                    return BadRequest();
            }

            if (result.IsFailure)
            {
                FlashError(result.Message);
            }
            else
            {
                Flash("Order " + result.Value.OrderNumber + " is now " + result.Value.Status + ".");
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        /* ---------------- notes and print ---------------- */

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(AddNoteModel model)
        {
            Result<Note> result = await _notes.AddAsync(NoteEntityType.Order, model.EntityId, model.Body);
            if (result.IsFailure)
            {
                FlashError(result.Message);
            }
            else
            {
                Flash("Note added.");
            }

            return RedirectToAction(nameof(Details), new { id = model.EntityId });
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id, string kind = "confirmation")
        {
            Order? order = await _orders.GetAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            var model = new OrderPrintModel
            {
                Order = order,
                Customer = await _customers.GetAsync(order.CustomerId),
                Kind = kind == "picklist" ? "picklist" : "confirmation"
            };

            return View(model);
        }

        /* ------------------------------------------------------------------ */

        /// <summary>Handles add/remove line postbacks. Returns true when the form should be re-rendered without saving.</summary>
        private async Task<bool> HandleLineActionAsync(OrderFormModel model, string? action)
        {
            if (string.IsNullOrEmpty(action) || action == SaveAction)
            {
                return false;
            }

            if (action == AddLineAction)
            {
                model.Input.Lines.Add(new OrderLineInput { Quantity = 1 });
            }
            else if (action.StartsWith(RemoveLinePrefix, StringComparison.Ordinal)
                     && int.TryParse(action.Substring(RemoveLinePrefix.Length), out int index)
                     && index >= 0 && index < model.Input.Lines.Count)
            {
                model.Input.Lines.RemoveAt(index);
            }

            // The indices changed, so the posted values must not be replayed over the model.
            ModelState.Clear();
            await PopulateAsync(model);
            return true;
        }

        private async Task PopulateAsync(OrderFormModel model)
        {
            PagedResult<Customer> customers = await _customers.SearchAsync(
                new CustomerFilter { Status = CustomerStatus.Active },
                new PagedRequest { PageSize = PagedRequest.MaxPageSize, SortBy = "name" });

            model.Customers = customers.Items
                .Select(c => new SelectListItem(c.CustomerNumber + "  " + c.Name, c.Id.ToString(), c.Id == model.Input.CustomerId))
                .ToList();

            IReadOnlyList<ProductLookup> products = await _products.LookupAsync(string.Empty, PagedRequest.MaxPageSize);
            model.Products = products
                .Select(p => new SelectListItem(p.Sku + "  " + p.Name + "  (" + p.UnitPrice.ToString("N2") + ")", p.Id.ToString()))
                .ToList();

            // Totals for whatever is valid so far; blank lines are ignored for the preview.
            var previewInput = new OrderInput
            {
                CustomerId = model.Input.CustomerId,
                OrderDate = model.Input.OrderDate,
                RequiredDate = model.Input.RequiredDate,
                CustomerReference = model.Input.CustomerReference,
                Notes = model.Input.Notes,
                ShipTo = model.Input.ShipTo,
                Lines = model.Input.Lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList()
            };

            if (previewInput.CustomerId > 0 && previewInput.Lines.Count > 0)
            {
                Result<Order> preview = await _orders.PreviewAsync(previewInput);
                model.Preview = preview.IsSuccess ? preview.Value : null;
            }
            else
            {
                model.Preview = null;
            }
        }

        private static OrderInput ToInput(Order order)
        {
            var input = new OrderInput
            {
                CustomerId = order.CustomerId,
                OrderDate = order.OrderDate,
                RequiredDate = order.RequiredDate,
                CustomerReference = order.CustomerReference,
                Notes = order.Notes,
                ShipTo = order.ShipTo?.Copy(),
                Lines = new List<OrderLineInput>()
            };

            foreach (OrderLine line in order.Lines)
            {
                input.Lines.Add(new OrderLineInput
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    Description = line.Description
                });
            }

            return input;
        }
    }
}
