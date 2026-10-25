
using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicManagementWeb.Pages;

public class PatientsModel : PageModel
{
    private readonly DatabaseService _database;

    public PatientsModel(DatabaseService database)
    {
        _database = database;
    }

    public List<Patient> Patients { get; set; } = new();

    [BindProperty]
    public PatientInput Input { get; set; } = new();

    [BindProperty]
    public int PatientId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPatientsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync();
            return Page();
        }

        await _database.AddPatientAsync(Input);
        TempData["SuccessMessage"] = "Patient registered successfully.";

        return RedirectToPage("/Patients");
    }

    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (id <= 0)
            return BadRequest();

        var patient = (await _database.GetPatientsAsync())
            .FirstOrDefault(p => p.PatientId == id);

        if (patient == null)
            return NotFound();

        PatientId = patient.PatientId;

        Input = new PatientInput
        {
            Name = patient.Name,
            Address = patient.Address,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Phone = patient.Phone
        };

        await LoadPatientsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (PatientId <= 0)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync();
            return Page();
        }

        try
        {
            await _database.UpdatePatientAsync(PatientId, Input);
            TempData["SuccessMessage"] = "Patient updated successfully.";
            return RedirectToPage("/Patients");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadPatientsAsync();
            return Page();
        }
        catch (MySqlConnector.MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to update this patient. Please try again.");

            await LoadPatientsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (id <= 0)
            return BadRequest();

        try
        {
            await _database.DeletePatientAsync(id);
            TempData["SuccessMessage"] = "Patient deleted successfully.";
        }
        catch (MySqlConnector.MySqlException)
        {
            TempData["ErrorMessage"] =
                "This patient cannot be deleted because related records may exist.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage("/Patients");
    }

    private async Task LoadPatientsAsync()
    {
        Patients = await _database.GetPatientsAsync();
    }
}
