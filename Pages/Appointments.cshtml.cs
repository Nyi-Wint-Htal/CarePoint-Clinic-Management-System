using ClinicManagementWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicManagementWeb.Pages;

public class AppointmentsModel : PageModel
{
    private readonly DatabaseService _database;

    public AppointmentsModel(DatabaseService database)
    {
        _database = database;
    }

    public List<Appointment> AppointmentsList { get; set; } = new();

    public List<Patient> Patients { get; set; } = new();

    public List<Doctor> Doctors { get; set; } = new();

    [BindProperty]
    public AppointmentInput Input { get; set; } = new()
    {
        AppointmentDate = DateTime.Today,
        AppointmentTime = new TimeSpan(9, 0, 0),
        Status = "Scheduled"
    };

    [BindProperty]
    public int AppointmentId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadFormDataAsync();
    }

   
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync();
            return Page();
        }

        bool hasConflict = await _database.HasAppointmentConflictAsync(
            Input.DoctorId,
            Input.AppointmentDate,
            Input.AppointmentTime);
        
        if (hasConflict)
        {
            ModelState.AddModelError(
                string.Empty,
                "This doctor already has an appointment at the selected date and time.");

            await LoadFormDataAsync();
            return Page();
        }
        

        await _database.AddAppointmentAsync(Input);

        TempData["SuccessMessage"] = "Appointment created successfully.";

        return RedirectToPage();
    }


   
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        try
        {
            await _database.DeleteAppointmentAsync(id);

            TempData["SuccessMessage"] = "Appointment deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (MySqlConnector.MySqlException)
        {
            TempData["ErrorMessage"] =
                "This appointment cannot be deleted because related records exist.";
        }

        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        AppointmentId = id;

        await LoadFormDataAsync();

        var appointment = AppointmentsList
            .FirstOrDefault(a => a.AppointmentId == id);

        if (appointment == null)
        {
            return NotFound();
        }

        Input = new AppointmentInput
        {
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            AppointmentDate = appointment.AppointmentDate,
            AppointmentTime = appointment.AppointmentTime,
            Status = appointment.Status,
            Reason = appointment.Reason
        };

        return Page();
    }

    
    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (AppointmentId <= 0)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync();
            return Page();
        }

        bool hasConflict = await _database.HasAppointmentConflictAsync(
            Input.DoctorId,
            Input.AppointmentDate,
            Input.AppointmentTime,
            AppointmentId);

        if (hasConflict)
        {
            ModelState.AddModelError(
                string.Empty,
                "This doctor already has an appointment at the selected date and time.");

            await LoadFormDataAsync();
            return Page();
        }

        await _database.UpdateAppointmentAsync(AppointmentId, Input);

        TempData["SuccessMessage"] = "Appointment updated successfully.";

        return RedirectToPage();
    }

public async Task<IActionResult> OnPostCompleteAsync(int id)
{
    if (id <= 0)
    {
        return BadRequest();
    }

    var appointments = await _database.GetAppointmentsAsync();
    var appointment = appointments.FirstOrDefault(a => a.AppointmentId == id);

    if (appointment == null)
    {
        return NotFound();
    }

    if (!string.Equals(
        appointment.Status, "Scheduled",
        StringComparison.OrdinalIgnoreCase))
    {
        TempData["ErrorMessage"] =
            "Only scheduled appointments can be marked as completed.";
        return RedirectToPage();
    }

    var input = new AppointmentInput
    {
        PatientId = appointment.PatientId,
        DoctorId = appointment.DoctorId,
        AppointmentDate = appointment.AppointmentDate,
        AppointmentTime = appointment.AppointmentTime,
        Status = "Completed",
        Reason = appointment.Reason
    };

    await _database.UpdateAppointmentAsync(id, input);

    TempData["SuccessMessage"] = "Appointment marked as completed.";
    return RedirectToPage();
}

public async Task<IActionResult> OnPostCancelAsync(int id)
{
    if (id <= 0)
    {
        return BadRequest();
    }

    var appointments = await _database.GetAppointmentsAsync();
    var appointment = appointments.FirstOrDefault(a => a.AppointmentId == id);

    if (appointment == null)
    {
        return NotFound();
    }

    if (!string.Equals(
        appointment.Status, "Scheduled",
        StringComparison.OrdinalIgnoreCase))
    {
        TempData["ErrorMessage"] =
            "Only scheduled appointments can be cancelled.";
        return RedirectToPage();
    }

    var input = new AppointmentInput
    {
        PatientId = appointment.PatientId,
        DoctorId = appointment.DoctorId,
        AppointmentDate = appointment.AppointmentDate,
        AppointmentTime = appointment.AppointmentTime,
        Status = "Cancelled",
        Reason = appointment.Reason
    };

    await _database.UpdateAppointmentAsync(id, input);

    TempData["SuccessMessage"] = "Appointment cancelled successfully.";
    return RedirectToPage();
}


    private async Task LoadFormDataAsync()
    {
        AppointmentsList = await _database.GetAppointmentsAsync();
        Patients = await _database.GetPatientsAsync();
        Doctors = await _database.GetDoctorsAsync();
    }
}