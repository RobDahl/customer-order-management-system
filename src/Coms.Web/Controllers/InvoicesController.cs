using System;
using System.Threading.Tasks;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Notes;
using Coms.Application.Orders;
using Coms.Domain.Common;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.CanView)]
    public class InvoicesController : ComsController
    {
        private readonly IInvoiceService _invoices;
        private readonly IOrderService _orders;
        private readonly ICustomerService _customers;
        private readonly INoteService _notes;

        public InvoicesController(IInvoiceService invoices, IOrderService orders, ICustomerService customers, INoteService notes)
        {
            _invoices = invoices;
            _orders = orders;
            _customers = customers;
            _notes = notes;
        }

        [HttpGet]
        public async Task<IActionResult> Index(InvoiceListModel query)
        {
            query.Results = await _invoices.SearchAsync(query.ToFilter(), query.ToPagedRequest());
            return View(query);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            Invoice? invoice = await _invoices.GetAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            var model = new InvoiceDetailsModel
            {
                Invoice = invoice,
                Order = await _orders.GetAsync(invoice.OrderId),
                Customer = await _customers.GetAsync(invoice.CustomerId),
                Notes = await _notes.GetAsync(NoteEntityType.Invoice, id)
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> CreateFromOrder(int orderId)
        {
            Order? order = await _orders.GetAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Fulfilled)
            {
                FlashError("Order " + order.OrderNumber + " is " + order.Status + "; only fulfilled orders can be invoiced.");
                return RedirectToAction("Details", "Orders", new { id = orderId });
            }

            return View(new CreateInvoiceModel
            {
                OrderId = orderId,
                RowVersion = Convert.ToBase64String(order.RowVersion),
                Order = order,
                Customer = await _customers.GetAsync(order.CustomerId)
            });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromOrder(CreateInvoiceModel model)
        {
            Result<Invoice> result = await _invoices.CreateFromOrderAsync(model.OrderId, ParseRowVersion(model.RowVersion), model.IssuedDate);
            if (result.IsFailure)
            {
                AddErrors(result);
                model.Order = await _orders.GetAsync(model.OrderId);
                model.Customer = model.Order == null ? null : await _customers.GetAsync(model.Order.CustomerId);
                return View(model);
            }

            Flash("Invoice " + result.Value.InvoiceNumber + " issued.");
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Payment(int id)
        {
            Invoice? invoice = await _invoices.GetAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            if (!invoice.IsOpen)
            {
                FlashError("Invoice " + invoice.InvoiceNumber + " is " + invoice.Status + " and cannot accept payments.");
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(new PaymentModel
            {
                InvoiceId = id,
                Amount = invoice.Balance,
                RowVersion = Convert.ToBase64String(invoice.RowVersion),
                Invoice = invoice
            });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(PaymentModel model)
        {
            var payment = new Payment
            {
                InvoiceId = model.InvoiceId,
                Amount = model.Amount,
                PaidDate = model.PaidDate,
                Method = model.Method,
                Reference = model.Reference,
                Notes = model.Notes
            };

            Result<Invoice> result = await _invoices.ApplyPaymentAsync(payment, ParseRowVersion(model.RowVersion));
            if (result.IsFailure)
            {
                AddErrors(result);
                model.Invoice = await _invoices.GetAsync(model.InvoiceId);
                return View(model);
            }

            Flash("Payment of " + payment.Amount.ToString("N2") + " recorded. Invoice is now " + result.Value.Status + ".");
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Void(int id)
        {
            Invoice? invoice = await _invoices.GetAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(new VoidInvoiceModel { Id = id, RowVersion = Convert.ToBase64String(invoice.RowVersion), Invoice = invoice });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Void(VoidInvoiceModel model)
        {
            Result<Invoice> result = await _invoices.VoidAsync(model.Id, ParseRowVersion(model.RowVersion), model.Comment);
            if (result.IsFailure)
            {
                FlashError(result.Message);
            }
            else
            {
                Flash("Invoice " + result.Value.InvoiceNumber + " voided. The order is fulfilled again and can be re-invoiced.");
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(AddNoteModel model)
        {
            Result<Note> result = await _notes.AddAsync(NoteEntityType.Invoice, model.EntityId, model.Body);
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
        public async Task<IActionResult> Print(int id)
        {
            Invoice? invoice = await _invoices.GetAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(new InvoicePrintModel
            {
                Invoice = invoice,
                Order = await _orders.GetAsync(invoice.OrderId),
                Customer = await _customers.GetAsync(invoice.CustomerId)
            });
        }
    }
}
