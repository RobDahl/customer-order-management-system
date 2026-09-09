using System.Threading.Tasks;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Notes;
using Coms.Application.Orders;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.CanView)]
    public class CustomersController : ComsController
    {
        private readonly ICustomerService _customers;
        private readonly IOrderService _orders;
        private readonly IInvoiceService _invoices;
        private readonly INoteService _notes;

        public CustomersController(ICustomerService customers, IOrderService orders, IInvoiceService invoices, INoteService notes)
        {
            _customers = customers;
            _orders = orders;
            _invoices = invoices;
            _notes = notes;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CustomerListModel query)
        {
            query.Results = await _customers.SearchAsync(query.ToFilter(), query.ToPagedRequest());
            return View(query);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            Customer? customer = await _customers.GetAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var model = new CustomerDetailsModel
            {
                Customer = customer,
                Balance = await _customers.GetBalanceAsync(id),
                RecentOrders = (await _orders.SearchAsync(new OrderFilter { CustomerId = id }, PagedRequest.FirstPage(10))).Items,
                RecentInvoices = await _invoices.GetForCustomerAsync(id),
                Notes = await _notes.GetAsync(NoteEntityType.Customer, id)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Statement(int id)
        {
            Customer? customer = await _customers.GetAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var model = new StatementModel
            {
                Customer = customer,
                Balance = await _customers.GetBalanceAsync(id),
                Invoices = await _invoices.GetForCustomerAsync(id),
                Payments = await _invoices.GetPaymentsForCustomerAsync(id)
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public IActionResult Create()
        {
            return View(new Customer { ShippingAddress = new Address() });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            Result<Customer> result = await _customers.CreateAsync(customer);
            if (result.IsFailure)
            {
                AddErrors(result);
                customer.ShippingAddress ??= new Address();
                return View(customer);
            }

            Flash("Customer " + result.Value.CustomerNumber + " created.");
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Edit(int id)
        {
            Customer? customer = await _customers.GetAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            customer.ShippingAddress ??= new Address();
            return View(customer);
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            customer.Id = id;

            Result<Customer> result = await _customers.UpdateAsync(customer);
            if (result.IsFailure)
            {
                AddErrors(result);
                customer.ShippingAddress ??= new Address();
                return View(customer);
            }

            Flash("Customer " + result.Value.CustomerNumber + " saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(ChangeStatusModel model)
        {
            Result<Customer> result = await _customers.ChangeStatusAsync(model.Id, model.Status, ParseRowVersion(model.RowVersion));
            if (result.IsFailure)
            {
                FlashError(result.Message);
            }
            else
            {
                Flash("Customer " + result.Value.CustomerNumber + " is now " + result.Value.Status + ".");
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(AddNoteModel model)
        {
            Result<Note> result = await _notes.AddAsync(NoteEntityType.Customer, model.EntityId, model.Body);
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
    }
}
