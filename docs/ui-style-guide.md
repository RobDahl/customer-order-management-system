# UI Style Guide

The user interface is deliberately plain: an industrial, form-and-grid
application in the tradition of internal line-of-business software. The goal
is that a warehouse clerk, an accounts assistant or an operations manager can
use it all day without noticing it. Nothing here should look like a marketing
site or a consumer app.

## Principles

1. Density over whitespace. Screens show as much relevant data as fits.
2. Text and tables, not cards and tiles.
3. One consistent layout for every screen: toolbar, filters, grid, detail.
4. Standard controls with default behaviour. No custom-drawn widgets.
5. No animation, no gradients, no shadows, no rounded panels, no hero areas.
6. Colour communicates state (status, error, warning). It is never decorative.
7. Every screen prints acceptably in black and white.

## Web (ASP.NET Core MVC)

### Layout

- Fixed top bar: application name (left), module menu (centre), signed-in
  user and role (right). Height 36px. Background navy `#1F3A5F`, text white.
- Left navigation column, 200px, light grey `#E9E9E9`, plain text links
  grouped under small uppercase headings. Current page underlined, bold.
- Content area: white background, 12px padding, page title in 18px bold,
  breadcrumb line under it in 12px grey.
- Footer status bar: 24px, grey `#E9E9E9`, shows environment name, database
  name, application version, server time.

### Typography

- Font stack: `"Segoe UI", Tahoma, Arial, sans-serif`. Body 13px, line-height 1.35.
- Headings: 18px (page), 14px bold (section). No larger sizes.
- Monospace for order numbers, SKUs, invoice numbers and money columns:
  `Consolas, "Courier New", monospace`.

### Colour

| Purpose         | Value                                                  |
| --------------- | ------------------------------------------------------ |
| Page background | `#F4F4F4`                                              |
| Panel / grid    | `#FFFFFF`                                              |
| Borders         | `#C8C8C8`                                              |
| Body text       | `#222222`                                              |
| Muted text      | `#666666`                                              |
| Link            | `#0B5394`                                              |
| Primary button  | `#1F3A5F` background, white text                       |
| Default button  | `#E1E1E1` background, `#222222` text, `#ADADAD` border |
| Success state   | `#2E7D32` text or 4px left border                      |
| Warning state   | `#B26A00`                                              |
| Error state     | `#B00020`                                              |
| Zebra row       | `#F7F7F7`                                              |
| Selected row    | `#D9E4F2`                                              |

### Tables

- `<table>` with 1px `#C8C8C8` borders, 4px 8px cell padding, header row
  grey `#E9E9E9` bold, sticky at top when the grid scrolls.
- Numbers and money right-aligned in monospace. Dates `yyyy-MM-dd`.
- Sortable columns show a plain `▲` / `▼` character. No icon fonts.
- Pager below the grid: "Showing 1-50 of 1,234" plus First / Prev / Next / Last.

### Forms

- Two-column grid: label right-aligned in a 160px column, control in the next.
- Inputs: 1px `#ADADAD` border, 2px radius, 4px 6px padding, 13px text.
- Required fields marked with a trailing `*` in the label.
- Validation message in red under the control, plus a summary at the top.
- Buttons in a right-aligned row at the bottom: Save (primary), Cancel.

### Components to avoid

Modals for anything except confirmations. Toasts. Icon-only buttons.
Client-side single-page frameworks. CSS frameworks that impose a look
(Bootstrap, Tailwind, Material). Web fonts. Charts other than plain
horizontal bar tables rendered as HTML.

## Desktop (WinForms, .NET Framework 4.8)

- Standard Windows visual styles enabled. Segoe UI 9pt throughout.
- Main window: `MenuStrip` (File, Customers, Products, Orders, Invoices,
  Reports, Tools, Help), `ToolStrip` with text buttons, `StatusStrip` with
  user, database and record count panels.
- Lists use `DataGridView` with read-only cells, full-row select, alternating
  row colour `#F7F7F7`, column headers left as default system rendering.
- Detail screens are separate modal forms opened from the grid, laid out with
  `TableLayoutPanel`: labels in column 0, controls in column 1, OK/Cancel
  bottom-right, `AcceptButton` and `CancelButton` set.
- Search/filter bar above every grid: `TextBox`, `ComboBox` for status,
  `Button` "Search", `Button` "Clear".
- Money columns right-aligned with `N2` format. Dates `yyyy-MM-dd`.
- Errors via `MessageBox.Show` with `MessageBoxIcon.Error`. Confirmations via
  `MessageBoxButtons.YesNo`.
- Printing via `PrintDocument` and `PrintPreviewDialog`. Plain text layout,
  no graphics.
- No custom painting, no owner-draw, no third-party control suites, no
  transparency, no animations, no images beyond the application icon.

## Reference

Look at classic ERP and accounting packages (Sage 50, Dynamics GP, SAP GUI,
QuickBooks Desktop, older Access applications) for the intended feel.
