
using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicManagementWeb.Pages;

public class MedicalHistoryModel : PageModel
{
    private readonly DatabaseService _database;

    public MedicalHistoryModel(DatabaseService database)
    {
        _database = database;
    }

    public List<MedicalHistory> Records { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();

    [BindProperty]
    public int MedicalHistoryId { get; set; }

    [BindProperty]
    public MedicalHistoryInput Input { get; set; } = new()
    {
        MedicalHistoryDate = DateTime.Today
    };

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    // CREATE
    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.AppointmentId <= 0)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "Please select an appointment.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var existingRecords = await _database.GetMedicalHistoryAsync();

        var duplicateExists = existingRecords.Any(
            record => record.AppointmentId == Input.AppointmentId);

        if (duplicateExists)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "This appointment already has a medical history record. Please select another appointment.");

            await LoadDataAsync();
            return Page();
        }

        try
        {
            await _database.AddMedicalHistoryAsync(Input);

            TempData["SuccessMessage"] =
                "Medical history record created successfully.";

            return RedirectToPage();
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "A medical history record already exists for this appointment.");

            await LoadDataAsync();
            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to save the medical record. Please try again.");

            await LoadDataAsync();
            return Page();
        }
    }

    // LOAD RECORDS AND APPOINTMENTS
    private async Task LoadDataAsync()
    {
        Records = await _database.GetMedicalHistoryAsync();
        Appointments = await _database.GetAppointmentsAsync();
    }

    // LOAD RECORD FOR EDITING
    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        await LoadDataAsync();

        var record = Records.FirstOrDefault(
            r => r.MedicalHistoryId == id);

        if (record == null)
        {
            return NotFound();
        }

        MedicalHistoryId = record.MedicalHistoryId;

        Input = new MedicalHistoryInput
        {
            AppointmentId = record.AppointmentId,
            MedicalHistoryDate =
                record.MedicalHistoryDate ?? DateTime.Today,
            Notes = record.Notes,
            DiagnosisDetails = record.DiagnosisDetails,
            TreatmentDetails = record.TreatmentDetails
        };

        return Page();
    }

    // UPDATE
    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (MedicalHistoryId <= 0)
        {
            return BadRequest();
        }

        if (Input.AppointmentId <= 0)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "Please select an appointment.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var existingRecords = await _database.GetMedicalHistoryAsync();

        var duplicateExists = existingRecords.Any(
            record =>
                record.AppointmentId == Input.AppointmentId &&
                record.MedicalHistoryId != MedicalHistoryId);

        if (duplicateExists)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "This appointment already has another medical history record.");

            await LoadDataAsync();
            return Page();
        }

        try
        {
            await _database.UpdateMedicalHistoryAsync(
                MedicalHistoryId, Input);

            TempData["SuccessMessage"] =
                "Medical history record updated successfully.";

            return RedirectToPage();
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            ModelState.AddModelError(
                "Input.AppointmentId",
                "This appointment already has a medical history record.");

            await LoadDataAsync();
            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to update the medical record. Please try again.");

            await LoadDataAsync();
            return Page();
        }
    }

    // DELETE
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        try
        {
            await _database.DeleteMedicalHistoryAsync(id);

            TempData["SuccessMessage"] =
                "Medical history record deleted successfully.";
        }
        catch (MySqlConnector.MySqlException)
        {
            TempData["ErrorMessage"] =
                "This record cannot be deleted because related records may exist.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }
}
