
using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicManagementWeb.Pages;

public class MedicinesModel : PageModel
{
    private readonly DatabaseService _database;

    public MedicinesModel(DatabaseService database)
    {
        _database = database;
    }

    public List<Medicine> MedicinesList { get; set; } = new();

    [BindProperty]
    public MedicineInput Input { get; set; } = new();

    [BindProperty]
    public int RestockMedicineId { get; set; }

    [BindProperty]
    public int RestockQuantity { get; set; }

    public async Task OnGetAsync()
    {
        await LoadMedicinesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(
                "Input.Name",
                "Medicine name is required.");
        }

        if (Input.StockQuantity < 0)
        {
            ModelState.AddModelError(
                "Input.StockQuantity",
                "Stock quantity cannot be negative.");
        }

        if (!ModelState.IsValid)
        {
            await LoadMedicinesAsync();
            return Page();
        }

        await _database.AddMedicineAsync(Input);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestockAsync()
    {
        if (RestockMedicineId <= 0)
        {
            ModelState.AddModelError(
                nameof(RestockMedicineId),
                "Please select a medicine.");
        }

        if (RestockQuantity <= 0)
        {
            ModelState.AddModelError(
                nameof(RestockQuantity),
                "Restock quantity must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            await LoadMedicinesAsync();
            return Page();
        }

        try
        {
            await _database.RestockMedicineAsync(
                RestockMedicineId,
                RestockQuantity);

            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadMedicinesAsync();
            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to restock the medicine. Please try again.");

            await LoadMedicinesAsync();
            return Page();
        }
    }

    private async Task LoadMedicinesAsync()
    {
        MedicinesList = await _database.GetMedicinesAsync();
    }
}
