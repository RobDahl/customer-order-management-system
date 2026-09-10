using System;
using System.Collections.Generic;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;

namespace Coms.Desktop.Printing
{
    /// <summary>Builds the text lines for the printable documents.</summary>
    internal static class Documents
    {
        public static List<string> OrderConfirmation(Order order, Customer customer)
        {
            var lines = new List<string>();
            Header(lines, "ORDER CONFIRMATION");
            OrderHeader(lines, order, customer);

            lines.Add(TextLayout.Left("#", 3) + TextLayout.Left("SKU", 12) + TextLayout.Left("Description", 37) + TextLayout.Right("Qty", 9) + TextLayout.Right("Price", 11) + TextLayout.Right("Disc%", 7) + TextLayout.Right("Total", 13));
            lines.Add(TextLayout.Rule());

            foreach (OrderLine line in order.Lines)
            {
                lines.Add(
                    TextLayout.Left(line.LineNumber.ToString(), 3) +
                    TextLayout.Left(line.Sku, 12) +
                    TextLayout.Left(line.Description, 37) +
                    TextLayout.Right(line.Quantity.ToString("0.###"), 9) +
                    TextLayout.Right(line.UnitPrice.ToString("N2"), 11) +
                    TextLayout.Right(line.DiscountPercent == 0 ? string.Empty : line.DiscountPercent.ToString("0.##"), 7) +
                    TextLayout.Right(line.LineTotal.ToString("N2"), 13));
            }

            lines.Add(TextLayout.Rule());
            lines.Add(TextLayout.Right("Subtotal", 79) + TextLayout.Right(order.Subtotal.ToString("N2"), 13));
            lines.Add(TextLayout.Right("Tax (" + (order.TaxRate * 100).ToString("0.##") + "%)", 79) + TextLayout.Right(order.TaxAmount.ToString("N2"), 13));
            lines.Add(TextLayout.Right("TOTAL", 79) + TextLayout.Right(order.Total.ToString("N2"), 13));

            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                lines.Add(string.Empty);
                lines.Add("Notes:");
                lines.AddRange(TextLayout.Wrap(order.Notes, TextLayout.Width));
            }

            Footer(lines);
            return lines;
        }

        public static List<string> PickList(Order order, Customer customer)
        {
            var lines = new List<string>();
            Header(lines, "PICK LIST");
            OrderHeader(lines, order, customer);

            lines.Add(TextLayout.Left("#", 3) + TextLayout.Left("SKU", 12) + TextLayout.Left("Description", 45) + TextLayout.Right("Qty", 9) + "  " + TextLayout.Left("Picked", 9) + TextLayout.Left("Location", 12));
            lines.Add(TextLayout.Rule());

            foreach (OrderLine line in order.Lines)
            {
                lines.Add(
                    TextLayout.Left(line.LineNumber.ToString(), 3) +
                    TextLayout.Left(line.Sku, 12) +
                    TextLayout.Left(line.Description, 45) +
                    TextLayout.Right(line.Quantity.ToString("0.###"), 9) +
                    "  [_______] [__________]");
            }

            lines.Add(TextLayout.Rule());
            lines.Add(string.Empty);
            lines.Add("Picked by: ______________________   Checked by: ______________________   Date: ____________");

            Footer(lines);
            return lines;
        }

        public static List<string> Invoice(Invoice invoice, Order order, Customer customer)
        {
            var lines = new List<string>();
            Header(lines, invoice.Status == InvoiceStatus.Void ? "VOID INVOICE" : "INVOICE");

            lines.Add(TextLayout.Left("Invoice:   " + invoice.InvoiceNumber, 46) + "Order:      " + (order?.OrderNumber ?? string.Empty));
            lines.Add(TextLayout.Left("Issued:    " + invoice.IssuedDate.ToString("yyyy-MM-dd"), 46) + "Reference:  " + (order?.CustomerReference ?? string.Empty));
            lines.Add(TextLayout.Left("Due:       " + invoice.DueDate.ToString("yyyy-MM-dd"), 46) + "Status:     " + invoice.Status);
            lines.Add(string.Empty);

            if (customer != null)
            {
                lines.Add("Bill to:");
                lines.Add("  " + customer.CustomerNumber + "  " + customer.Name);
                foreach (string part in AddressLines(customer.BillingAddress))
                {
                    lines.Add("  " + part);
                }
            }

            lines.Add(string.Empty);
            lines.Add(TextLayout.Left("#", 3) + TextLayout.Left("SKU", 12) + TextLayout.Left("Description", 37) + TextLayout.Right("Qty", 9) + TextLayout.Right("Price", 11) + TextLayout.Right("Disc%", 7) + TextLayout.Right("Total", 13));
            lines.Add(TextLayout.Rule());

            if (order != null)
            {
                foreach (OrderLine line in order.Lines)
                {
                    lines.Add(
                        TextLayout.Left(line.LineNumber.ToString(), 3) +
                        TextLayout.Left(line.Sku, 12) +
                        TextLayout.Left(line.Description, 37) +
                        TextLayout.Right(line.Quantity.ToString("0.###"), 9) +
                        TextLayout.Right(line.UnitPrice.ToString("N2"), 11) +
                        TextLayout.Right(line.DiscountPercent == 0 ? string.Empty : line.DiscountPercent.ToString("0.##"), 7) +
                        TextLayout.Right(line.LineTotal.ToString("N2"), 13));
                }
            }

            lines.Add(TextLayout.Rule());
            lines.Add(TextLayout.Right("Subtotal", 79) + TextLayout.Right(invoice.Subtotal.ToString("N2"), 13));
            lines.Add(TextLayout.Right("Tax", 79) + TextLayout.Right(invoice.TaxAmount.ToString("N2"), 13));
            lines.Add(TextLayout.Right("TOTAL", 79) + TextLayout.Right(invoice.Total.ToString("N2"), 13));
            lines.Add(TextLayout.Right("Paid", 79) + TextLayout.Right(invoice.AmountPaid.ToString("N2"), 13));
            lines.Add(TextLayout.Right("BALANCE DUE", 79) + TextLayout.Right(invoice.Balance.ToString("N2"), 13));
            lines.Add(string.Empty);
            lines.Add("Payment due by " + invoice.DueDate.ToString("yyyy-MM-dd") + ". Please quote the invoice number with your payment.");

            Footer(lines);
            return lines;
        }

        private static void Header(List<string> lines, string title)
        {
            lines.Add("CUSTOMER ORDER MANAGEMENT" + TextLayout.Right(title, TextLayout.Width - 25));
            lines.Add("Internal operations");
            lines.Add(TextLayout.Rule('='));
            lines.Add(string.Empty);
        }

        private static void OrderHeader(List<string> lines, Order order, Customer customer)
        {
            lines.Add(TextLayout.Left("Order:     " + order.OrderNumber, 46) + "Status:     " + order.Status);
            lines.Add(TextLayout.Left("Date:      " + order.OrderDate.ToString("yyyy-MM-dd"), 46) + "Required:   " + (order.RequiredDate.HasValue ? order.RequiredDate.Value.ToString("yyyy-MM-dd") : string.Empty));
            lines.Add(TextLayout.Left("Reference: " + (order.CustomerReference ?? string.Empty), 46));
            lines.Add(string.Empty);

            var left = new List<string> { "Customer:" };
            if (customer != null)
            {
                left.Add("  " + customer.CustomerNumber + "  " + customer.Name);
                if (!string.IsNullOrEmpty(customer.ContactName)) { left.Add("  " + customer.ContactName); }
                if (!string.IsNullOrEmpty(customer.Email)) { left.Add("  " + customer.Email); }
                if (!string.IsNullOrEmpty(customer.Phone)) { left.Add("  " + customer.Phone); }
            }

            var right = new List<string> { "Ship to:" };
            if (order.ShipTo != null)
            {
                foreach (string part in AddressLines(order.ShipTo))
                {
                    right.Add("  " + part);
                }
            }

            int rows = Math.Max(left.Count, right.Count);
            for (int i = 0; i < rows; i++)
            {
                lines.Add(TextLayout.Left(i < left.Count ? left[i] : string.Empty, 46) + (i < right.Count ? right[i] : string.Empty));
            }

            lines.Add(string.Empty);
        }

        private static IEnumerable<string> AddressLines(Coms.Domain.Common.Address address)
        {
            if (address == null)
            {
                yield break;
            }

            yield return address.Line1;
            if (!string.IsNullOrEmpty(address.Line2)) { yield return address.Line2; }
            yield return (address.City + " " + address.Region + " " + address.PostalCode).Trim();
            yield return address.Country;
        }

        private static void Footer(List<string> lines)
        {
            lines.Add(string.Empty);
            lines.Add(TextLayout.Rule());
            lines.Add("Printed " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " by " + Environment.UserName);
        }
    }
}
