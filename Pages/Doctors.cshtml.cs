
using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace ClinicManagementWeb.Pages;

public class DoctorsModel : PageModel
{
    private readonly DatabaseService _database;

    public DoctorsModel(DatabaseService database)
    {
        _database = database;
    }

    public List<Doctor> Doctors { get; set; } = new();

    [BindProperty]
    public DoctorInput Input { get; set; } = new();

    [BindProperty]
    public int DoctorId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDoctorsAsync();
    }

    // CREATE
    // CREATE
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDoctorsAsync();
            return Page();
        }

        try
        {
            await _database.AddDoctorAsync(Input);

            TempData["SuccessMessage"] = "Doctor registered successfully.";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to register this doctor. Please check the information and try again.");
        }

        await LoadDoctorsAsync();
        return Page();
    }
    // LOAD DOCTOR DETAILS FOR EDITING
    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (id <= 0)
            return BadRequest();

        var doctor = (await _database.GetDoctorsAsync())
            .FirstOrDefault(d => d.DoctorId == id);

        if (doctor == null)
            return NotFound();

        DoctorId = doctor.DoctorId;

        Input = new DoctorInput
        {
            Name = doctor.Name,
            Specialization = doctor.Specialization,
            Phone = doctor.Phone,
            Email = doctor.Email
        };

        await LoadDoctorsAsync();
        return Page();
    }

    // UPDATE
    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (DoctorId <= 0)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await LoadDoctorsAsync();
            return Page();
        }

        try
        {
            await _database.UpdateDoctorAsync(DoctorId, Input);

            TempData["SuccessMessage"] = "Doctor updated successfully.";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadDoctorsAsync();
            return Page();
        }
        catch (MySqlException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to update this doctor. Please try again.");

            await LoadDoctorsAsync();
            return Page();
        }
    }

    // DELETE
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (id <= 0)
            return BadRequest();

        try
        {
            await _database.DeleteDoctorAsync(id);
            TempData["SuccessMessage"] = "Doctor deleted successfully.";
        }
        catch (MySqlException)
        {
            TempData["ErrorMessage"] =
                "This doctor cannot be deleted because related records may exist.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task LoadDoctorsAsync()
    {
        Doctors = await _database.GetDoctorsAsync();
    }
}
