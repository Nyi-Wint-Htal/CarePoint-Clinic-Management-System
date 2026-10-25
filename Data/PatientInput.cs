namespace ClinicManagementWeb.Data;

public class PatientInput
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string Phone { get; set; } = "";
}