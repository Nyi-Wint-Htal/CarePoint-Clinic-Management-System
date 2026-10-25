
using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicManagementWeb.Pages;

public class PrescriptionsModel : PageModel
{
    private readonly DatabaseService _database;

    public PrescriptionsModel(DatabaseService database)
    {
        _database = database;
    }

    public List<MedicalHistory> AvailableMedicalHistories { get; set; } = new();

    public List<Prescription> PrescriptionsList { get; set; } = new();

    public List<Medicine> MedicinesList { get; set; } = new();

    public List<PrescriptionItem> PrescriptionItemsList { get; set; } = new();

    [BindProperty]
    public PrescriptionInput Input { get; set; } = new()
    {
        PrescriptionDate = DateTime.Today
    };

    [BindProperty]
    public PrescriptionItemInput ItemInput { get; set; } = new();

    public int? SelectedPrescriptionId { get; set; }

    // The currently selected prescription, for displaying patient and visit details.
    public Prescription? SelectedPrescription =>
        SelectedPrescriptionId.HasValue
            ? PrescriptionsList.FirstOrDefault(
                p => p.PrescriptionId == SelectedPrescriptionId.Value)
            : null;

    // DISPLAY PRESCRIPTIONS AND THEIR ITEMS
    public async Task OnGetAsync(int? prescriptionId)
    {
        SelectedPrescriptionId = prescriptionId;

        await LoadDataAsync();

        if (SelectedPrescriptionId.HasValue &&
            SelectedPrescriptionId.Value > 0)
        {
            PrescriptionItemsList =
                await _database.GetPrescriptionItemsAsync(
                    SelectedPrescriptionId.Value);
        }
    }

    // CREATE PRESCRIPTION
    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.MedicalHistoryId <= 0)
        {
            ModelState.AddModelError(
                "Input.MedicalHistoryId",
                "Please select a medical history record.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        try
        {
            await _database.AddPrescriptionAsync(Input);

            TempData["SuccessMessage"] =
                "Prescription created successfully.";

            return RedirectToPage();
        }
        catch (MySqlConnector.MySqlException ex)
            when (ex.Number == 1062)
        {
            ModelState.AddModelError(
                "Input.MedicalHistoryId",
                "This medical history record already has a prescription.");

            await LoadDataAsync();
            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to save the prescription. Please try again.");

            await LoadDataAsync();
            return Page();
        }
    }

    // ADD MEDICINE TO A PRESCRIPTION
    public async Task<IActionResult> OnPostAddItemAsync()
    {
        if (ItemInput.PrescriptionId <= 0)
        {
            ModelState.AddModelError(
                "ItemInput.PrescriptionId",
                "Please select a prescription.");
        }

        if (ItemInput.MedicineId <= 0)
        {
            ModelState.AddModelError(
                "ItemInput.MedicineId",
                "Please select a medicine.");
        }

        if (ItemInput.Quantity <= 0)
        {
            ModelState.AddModelError(
                "ItemInput.Quantity",
                "Quantity must be greater than zero.");
        }

        int prescriptionId = ItemInput.PrescriptionId;

        SelectedPrescriptionId =
            prescriptionId > 0 ? prescriptionId : null;

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();

            if (SelectedPrescriptionId.HasValue)
            {
                PrescriptionItemsList =
                    await _database.GetPrescriptionItemsAsync(
                        SelectedPrescriptionId.Value);
            }

            return Page();
        }

        try
        {
            await _database.AddPrescriptionItemAsync(ItemInput);

            TempData["SuccessMessage"] =
                "Medicine added to the prescription successfully.";

            return RedirectToPage(new { prescriptionId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await LoadDataAsync();

            PrescriptionItemsList =
                await _database.GetPrescriptionItemsAsync(
                    prescriptionId);

            return Page();
        }
        catch (MySqlConnector.MySqlException ex)
            when (ex.Number == 1062)
        {
            ModelState.AddModelError(
                string.Empty,
                "This medicine has already been added to this prescription.");

            await LoadDataAsync();

            PrescriptionItemsList =
                await _database.GetPrescriptionItemsAsync(
                    prescriptionId);

            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to add this medicine. Please check the selected records and try again.");

            await LoadDataAsync();

            PrescriptionItemsList =
                await _database.GetPrescriptionItemsAsync(
                    prescriptionId);

            return Page();
        }
    }

    // LOAD PAGE DATA
    private async Task LoadDataAsync()
    {
        AvailableMedicalHistories =
            await _database.GetMedicalHistoriesWithoutPrescriptionAsync();

        PrescriptionsList =
            await _database.GetPrescriptionsAsync();

        MedicinesList =
            await _database.GetMedicinesAsync();
    }
}
