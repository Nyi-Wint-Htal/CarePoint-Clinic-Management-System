using MySqlConnector;

namespace ClinicManagementWeb.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("ClinicDatabase")
            ?? throw new InvalidOperationException(
                "ClinicDatabase connection string is missing.");
    }

    public async Task<List<Patient>> GetPatientsAsync()
    {
        var patients = new List<Patient>();

        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            SELECT Patient_ID, P_Name, P_Address,
                   P_DOB, P_Gender, P_Phone
            FROM Patient
            ORDER BY Patient_ID;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            patients.Add(new Patient
            {
                PatientId = reader.GetInt32("Patient_ID"),
                Name = reader.GetString("P_Name"),
                Address = reader.IsDBNull(reader.GetOrdinal("P_Address"))
                    ? ""
                    : reader.GetString("P_Address"),
                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("P_DOB"))
                    ? null
                    : reader.GetDateTime("P_DOB"),
                Gender = reader.IsDBNull(reader.GetOrdinal("P_Gender"))
                    ? ""
                    : reader.GetString("P_Gender"),
                Phone = reader.IsDBNull(reader.GetOrdinal("P_Phone"))
                    ? ""
                    : reader.GetString("P_Phone")
            });
        }

        return patients;
    }
    
    public async Task AddPatientAsync(PatientInput input)
    {
        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
                           INSERT INTO Patient
                               (P_Name, P_Address, P_DOB, P_Gender, P_Phone)
                           VALUES
                               (@Name, @Address, @DateOfBirth, @Gender, @Phone);
                           """;

        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@Name", input.Name);
        command.Parameters.AddWithValue("@Address", input.Address);
        command.Parameters.AddWithValue(
            "@DateOfBirth",
            (object?)input.DateOfBirth?.Date ?? DBNull.Value);
        command.Parameters.AddWithValue("@Gender", input.Gender);
        command.Parameters.AddWithValue("@Phone", input.Phone);

        await command.ExecuteNonQueryAsync();
    }
    
    
public async Task UpdatePatientAsync(int patientId, PatientInput input)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
        UPDATE Patient
        SET P_Name = @Name,
            P_Address = @Address,
            P_DOB = @DateOfBirth,
            P_Gender = @Gender,
            P_Phone = @Phone
        WHERE Patient_ID = @PatientId;
        """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@PatientId", patientId);
    command.Parameters.AddWithValue("@Name", input.Name.Trim());
    command.Parameters.AddWithValue("@Address",
        (object?)input.Address ?? DBNull.Value);
    command.Parameters.AddWithValue("@DateOfBirth",
        (object?)input.DateOfBirth?.Date ?? DBNull.Value);
    command.Parameters.AddWithValue("@Gender",
        (object?)input.Gender ?? DBNull.Value);
    command.Parameters.AddWithValue("@Phone",
        (object?)input.Phone ?? DBNull.Value);

    int rowsAffected = await command.ExecuteNonQueryAsync();

    if (rowsAffected == 0)
    {
        // A valid update may report zero changed rows if the values
        // are unchanged, so verify that the patient exists.
        const string checkSql = """
            SELECT COUNT(*)
            FROM Patient
            WHERE Patient_ID = @PatientId;
            """;

        await using var checkCommand =
            new MySqlCommand(checkSql, connection);

        checkCommand.Parameters.AddWithValue("@PatientId", patientId);

        var exists = Convert.ToInt32(
            await checkCommand.ExecuteScalarAsync());

        if (exists == 0)
        {
            throw new InvalidOperationException(
                "Patient not found.");
        }
    }
}

public async Task DeletePatientAsync(int patientId)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
        DELETE FROM Patient
        WHERE Patient_ID = @PatientId;
        """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@PatientId", patientId);

    int rowsAffected = await command.ExecuteNonQueryAsync();

    if (rowsAffected == 0)
    {
        throw new InvalidOperationException(
            "Patient not found.");
    }
}

    
    public async Task<List<Appointment>> GetAppointmentsAsync()
{
    var appointments = new List<Appointment>();

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       SELECT
                           a.Appointment_ID,
                           a.Patient_ID,
                           a.Doctor_ID,
                           p.P_Name AS PatientName,
                           d.D_Name AS DoctorName,
                           a.Appointment_Date,
                           a.Appointment_Time,
                           a.Status,
                           a.Reason
                       FROM Appointment a
                       INNER JOIN Patient p
                           ON a.Patient_ID = p.Patient_ID
                       INNER JOIN Doctor d
                           ON a.Doctor_ID = d.Doctor_ID
                       ORDER BY
                           a.Appointment_Date DESC,
                           a.Appointment_Time DESC;
                       """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        appointments.Add(new Appointment
        {
            AppointmentId = reader.GetInt32("Appointment_ID"),
            PatientId = reader.GetInt32("Patient_ID"),
            DoctorId = reader.GetInt32("Doctor_ID"),
            PatientName = reader.GetString("PatientName"),
            DoctorName = reader.GetString("DoctorName"),
            AppointmentDate = reader.GetDateTime("Appointment_Date"),
            AppointmentTime = reader.GetTimeSpan("Appointment_Time"),
            Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                ? "Scheduled"
                : reader.GetString("Status"),
            Reason = reader.IsDBNull(reader.GetOrdinal("Reason"))
                ? ""
                : reader.GetString("Reason")
        });
    }

    return appointments;
}

    public async Task<bool> HasAppointmentConflictAsync(
        int doctorId,
        DateTime appointmentDate,
        TimeSpan appointmentTime,
        int? excludeAppointmentId = null)
    {
        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
                           SELECT COUNT(*)
                           FROM Appointment
                           WHERE Doctor_ID = @DoctorId
                             AND Appointment_Date = @AppointmentDate
                             AND Appointment_Time = @AppointmentTime
                             AND (@ExcludeAppointmentId IS NULL
                                  OR Appointment_ID <> @ExcludeAppointmentId);
                           """;

        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@DoctorId", doctorId);
        command.Parameters.AddWithValue(
            "@AppointmentDate", appointmentDate.Date);
        command.Parameters.AddWithValue(
            "@AppointmentTime", appointmentTime);
        command.Parameters.AddWithValue(
            "@ExcludeAppointmentId",
            (object?)excludeAppointmentId ?? DBNull.Value);

        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync());

        return count > 0;
    }

public async Task AddAppointmentAsync(AppointmentInput input)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
        INSERT INTO Appointment
            (Patient_ID, Doctor_ID, Appointment_Date,
             Appointment_Time, Status, Reason)
        VALUES
            (@PatientId, @DoctorId, @AppointmentDate,
             @AppointmentTime, @Status, @Reason);
        """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@PatientId", input.PatientId);
    command.Parameters.AddWithValue("@DoctorId", input.DoctorId);
    command.Parameters.AddWithValue("@AppointmentDate", input.AppointmentDate.Date);
    command.Parameters.AddWithValue("@AppointmentTime", input.AppointmentTime);
    command.Parameters.AddWithValue("@Status", input.Status);
    command.Parameters.AddWithValue("@Reason", input.Reason ?? "");

    await command.ExecuteNonQueryAsync();
}
public async Task UpdateAppointmentAsync(int appointmentId, AppointmentInput input)
{
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();

    const string sql = """
                       UPDATE Appointment
                       SET Patient_ID = @PatientId,
                           Doctor_ID = @DoctorId,
                           Appointment_Date = @AppointmentDate,
                           Appointment_Time = @AppointmentTime,
                           Status = @Status,
                           Reason = @Reason
                       WHERE Appointment_ID = @AppointmentId;
                       """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@AppointmentId", appointmentId);
    command.Parameters.AddWithValue("@PatientId", input.PatientId);
    command.Parameters.AddWithValue("@DoctorId", input.DoctorId);
    command.Parameters.AddWithValue("@AppointmentDate", input.AppointmentDate.Date);
    command.Parameters.AddWithValue("@AppointmentTime", input.AppointmentTime);
    command.Parameters.AddWithValue("@Status", input.Status);
    command.Parameters.AddWithValue("@Reason", input.Reason ?? "");

    await command.ExecuteNonQueryAsync();
}

public async Task DeleteAppointmentAsync(int appointmentId)
{
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();

    const string sql = @"
        DELETE FROM Appointment
        WHERE Appointment_ID = @AppointmentId
          AND NOT EXISTS (
              SELECT 1
              FROM Medical_History
              WHERE Appointment_ID = @AppointmentId
          );";

    await using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@AppointmentId", appointmentId);

    var rowsAffected = await command.ExecuteNonQueryAsync();

    if (rowsAffected == 0)
    {
        throw new InvalidOperationException(
            "This appointment cannot be deleted because it has a medical history record, or it no longer exists.");
    }
}

    public async Task<List<Doctor>> GetDoctorsAsync()
    {
        var doctors = new List<Doctor>();

        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
                           SELECT Doctor_ID, D_Name, Specialization, D_Phone, D_Email
                           FROM Doctor
                           ORDER BY Doctor_ID;
                           """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            doctors.Add(new Doctor
            {
                DoctorId = reader.GetInt32("Doctor_ID"),
                Name = reader.GetString("D_Name"),
                Specialization = reader.IsDBNull(reader.GetOrdinal("Specialization"))
                    ? ""
                    : reader.GetString("Specialization"),
                Phone = reader.IsDBNull(reader.GetOrdinal("D_Phone"))
                    ? ""
                    : reader.GetString("D_Phone"),
                Email = reader.IsDBNull(reader.GetOrdinal("D_Email"))
                    ? ""
                    : reader.GetString("D_Email")
            });
        }

        return doctors;
    }

    public async Task AddDoctorAsync(DoctorInput input)
    {
        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
                           INSERT INTO Doctor
                               (D_Name, Specialization, D_Phone, D_Email)
                           VALUES
                               (@Name, @Specialization, @Phone, @Email);
                           """;

        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@Name", input.Name);
        command.Parameters.AddWithValue("@Specialization", input.Specialization);
        command.Parameters.AddWithValue("@Phone", input.Phone);
        command.Parameters.AddWithValue("@Email", input.Email);

        await command.ExecuteNonQueryAsync();
    }
    
    
    public async Task UpdateDoctorAsync(int doctorId, DoctorInput input)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
                           UPDATE Doctor
                           SET D_Name = @Name,
                               Specialization = @Specialization,
                               D_Phone = @Phone,
                               D_Email = @Email
                           WHERE Doctor_ID = @DoctorId;
                           """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DoctorId", doctorId);
        command.Parameters.AddWithValue("@Name", input.Name.Trim());
        command.Parameters.AddWithValue("@Specialization",
            (object?)input.Specialization ?? DBNull.Value);
        command.Parameters.AddWithValue("@Phone",
            (object?)input.Phone ?? DBNull.Value);
        command.Parameters.AddWithValue("@Email",
            (object?)input.Email ?? DBNull.Value);

        int rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
        {
            const string checkSql =
                "SELECT COUNT(*) FROM Doctor WHERE Doctor_ID = @DoctorId;";

            await using var checkCommand =
                new MySqlCommand(checkSql, connection);

            checkCommand.Parameters.AddWithValue("@DoctorId", doctorId);

            int exists = Convert.ToInt32(
                await checkCommand.ExecuteScalarAsync());

            if (exists == 0)
                throw new InvalidOperationException("Doctor not found.");
        }
    }

    public async Task DeleteDoctorAsync(int doctorId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql =
            "DELETE FROM Doctor WHERE Doctor_ID = @DoctorId;";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DoctorId", doctorId);

        int rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
            throw new InvalidOperationException("Doctor not found.");
    }

    
    public async Task<List<MedicalHistory>> GetMedicalHistoryAsync()
{
    var records = new List<MedicalHistory>();

    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();

    const string sql = """
        SELECT
            mh.MH_ID,
            mh.Appointment_ID,
            mh.MH_Date,
            mh.Notes,
            a.Appointment_Date,
            p.P_Name AS PatientName,
            d.D_Name AS DoctorName,
            mhd.Diagnosis_Details,
            mhd.Treatment_Details
        FROM Medical_History mh
        INNER JOIN Appointment a
            ON mh.Appointment_ID = a.Appointment_ID
        INNER JOIN Patient p
            ON a.Patient_ID = p.Patient_ID
        INNER JOIN Doctor d
            ON a.Doctor_ID = d.Doctor_ID
        LEFT JOIN Medical_History_Detail mhd
            ON mh.MH_ID = mhd.MH_ID
        ORDER BY mh.MH_Date DESC;
        """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        records.Add(new MedicalHistory
        {
            MedicalHistoryId = reader.GetInt32("MH_ID"),
            AppointmentId = reader.GetInt32("Appointment_ID"),
            MedicalHistoryDate = reader.IsDBNull(reader.GetOrdinal("MH_Date"))
                ? null
                : reader.GetDateTime("MH_Date"),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString("Notes"),
            AppointmentDate = reader.GetDateTime("Appointment_Date"),
            PatientName = reader.GetString("PatientName"),
            DoctorName = reader.GetString("DoctorName"),
            DiagnosisDetails = reader.IsDBNull(reader.GetOrdinal("Diagnosis_Details"))
                ? null
                : reader.GetString("Diagnosis_Details"),
            TreatmentDetails = reader.IsDBNull(reader.GetOrdinal("Treatment_Details"))
                ? null
                : reader.GetString("Treatment_Details")
        });
    }

    return records;
}

public async Task AddMedicalHistoryAsync(MedicalHistoryInput input)
{
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();

    await using var transaction = await connection.BeginTransactionAsync();

    try
    {
        const string historySql = """
            INSERT INTO Medical_History
                (Appointment_ID, MH_Date, Notes)
            VALUES
                (@AppointmentId, @MedicalHistoryDate, @Notes);
            """;

        int medicalHistoryId;

        await using (var command = new MySqlCommand(
            historySql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@AppointmentId", input.AppointmentId);

            command.Parameters.AddWithValue(
                "@MedicalHistoryDate", input.MedicalHistoryDate.Date);

            command.Parameters.AddWithValue(
                "@Notes", (object?)input.Notes ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();

            medicalHistoryId = (int)command.LastInsertedId;
        }

        const string detailSql = """
            INSERT INTO Medical_History_Detail
                (MH_ID, Diagnosis_Details, Treatment_Details)
            VALUES
                (@MedicalHistoryId, @Diagnosis, @Treatment);
            """;

        await using (var command = new MySqlCommand(
            detailSql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@MedicalHistoryId", medicalHistoryId);

            command.Parameters.AddWithValue(
                "@Diagnosis",
                (object?)input.DiagnosisDetails ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "@Treatment",
                (object?)input.TreatmentDetails ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

public async Task UpdateMedicalHistoryAsync(
    int medicalHistoryId,
    MedicalHistoryInput input)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    await using var transaction =
        await connection.BeginTransactionAsync();

    try
    {
        const string historySql = """
                                  UPDATE Medical_History
                                  SET MH_Date = @MedicalHistoryDate,
                                      Notes = @Notes
                                  WHERE MH_ID = @MedicalHistoryId;
                                  """;

        await using (var command = new MySqlCommand(
                         historySql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@MedicalHistoryId", medicalHistoryId);

            command.Parameters.AddWithValue(
                "@MedicalHistoryDate", input.MedicalHistoryDate.Date);

            command.Parameters.AddWithValue(
                "@Notes", (object?)input.Notes ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        const string detailSql = """
                                 INSERT INTO Medical_History_Detail
                                     (MH_ID, Diagnosis_Details, Treatment_Details)
                                 VALUES
                                     (@MedicalHistoryId, @Diagnosis, @Treatment)
                                 ON DUPLICATE KEY UPDATE
                                     Diagnosis_Details = @Diagnosis,
                                     Treatment_Details = @Treatment;
                                 """;

        await using (var command = new MySqlCommand(
                         detailSql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@MedicalHistoryId", medicalHistoryId);

            command.Parameters.AddWithValue(
                "@Diagnosis",
                (object?)input.DiagnosisDetails ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "@Treatment",
                (object?)input.TreatmentDetails ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
public async Task DeleteMedicalHistoryAsync(int medicalHistoryId)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    await using var transaction =
        await connection.BeginTransactionAsync();

    try
    {
        const string detailSql = """
                                 DELETE FROM Medical_History_Detail
                                 WHERE MH_ID = @MedicalHistoryId;
                                 """;

        await using (var command = new MySqlCommand(
                         detailSql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@MedicalHistoryId", medicalHistoryId);

            await command.ExecuteNonQueryAsync();
        }

        const string historySql = """
                                  DELETE FROM Medical_History
                                  WHERE MH_ID = @MedicalHistoryId;
                                  """;

        await using (var command = new MySqlCommand(
                         historySql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "@MedicalHistoryId", medicalHistoryId);

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
public async Task<List<Medicine>> GetMedicinesAsync()
{
    var medicines = new List<Medicine>();

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       SELECT Medicine_ID, Name, Type, Manufacturer, Unit, Stock_Quantity
                       FROM Medicine
                       ORDER BY Name;
                       """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        medicines.Add(new Medicine
        {
            MedicineId = reader.GetInt32("Medicine_ID"),
            Name = reader.GetString("Name"),
            Type = reader.IsDBNull(reader.GetOrdinal("Type"))
                ? null
                : reader.GetString("Type"),
            Manufacturer = reader.IsDBNull(reader.GetOrdinal("Manufacturer"))
                ? null
                : reader.GetString("Manufacturer"),
            Unit = reader.IsDBNull(reader.GetOrdinal("Unit"))
                ? null
                : reader.GetString("Unit"),
            StockQuantity = reader.IsDBNull(reader.GetOrdinal("Stock_Quantity"))
                ? 0
                : reader.GetInt32("Stock_Quantity")
        });
    }

    return medicines;
}
public async Task AddMedicineAsync(MedicineInput input)
{
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       INSERT INTO Medicine
                           (Name, Type, Manufacturer, Unit, Stock_Quantity)
                       VALUES
                           (@Name, @Type, @Manufacturer, @Unit, @StockQuantity);
                       """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@Name", input.Name.Trim());
    command.Parameters.AddWithValue(
        "@Type", (object?)input.Type ?? DBNull.Value);
    command.Parameters.AddWithValue(
        "@Manufacturer", (object?)input.Manufacturer ?? DBNull.Value);
    command.Parameters.AddWithValue(
        "@Unit", (object?)input.Unit ?? DBNull.Value);
    command.Parameters.AddWithValue(
        "@StockQuantity", input.StockQuantity);

    await command.ExecuteNonQueryAsync();
}

public async Task<List<MedicalHistory>> GetMedicalHistoriesWithoutPrescriptionAsync()
{
    var records = new List<MedicalHistory>();

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       SELECT
                           mh.MH_ID,
                           mh.Appointment_ID,
                           mh.MH_Date,
                           mh.Notes,
                           p.P_Name AS PatientName,
                           d.D_Name AS DoctorName
                       FROM Medical_History mh
                       INNER JOIN Appointment a
                           ON mh.Appointment_ID = a.Appointment_ID
                       INNER JOIN Patient p
                           ON a.Patient_ID = p.Patient_ID
                       INNER JOIN Doctor d
                           ON a.Doctor_ID = d.Doctor_ID
                       LEFT JOIN Prescription pr
                           ON mh.MH_ID = pr.MH_ID
                       WHERE pr.Prescription_ID IS NULL
                       ORDER BY mh.MH_Date DESC;
                       """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        records.Add(new MedicalHistory
        {
            MedicalHistoryId = reader.GetInt32("MH_ID"),
            AppointmentId = reader.GetInt32("Appointment_ID"),
            MedicalHistoryDate = reader.IsDBNull(reader.GetOrdinal("MH_Date"))
                ? null
                : reader.GetDateTime("MH_Date"),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString("Notes"),
            PatientName = reader.GetString("PatientName"),
            DoctorName = reader.GetString("DoctorName")
        });
    }

    return records;
}
public async Task AddPrescriptionAsync(PrescriptionInput input)
{
    
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       INSERT INTO Prescription
                           (MH_ID, Prescription_Date, Notes)
                       VALUES
                           (@MedicalHistoryId, @PrescriptionDate, @Notes);
                       """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue(
        "@MedicalHistoryId", input.MedicalHistoryId);

    command.Parameters.AddWithValue(
        "@PrescriptionDate", input.PrescriptionDate.Date);

    command.Parameters.AddWithValue(
        "@Notes", (object?)input.Notes ?? DBNull.Value);

    await command.ExecuteNonQueryAsync();
}
public async Task<List<Prescription>> GetPrescriptionsAsync()
{
    var prescriptions = new List<Prescription>();

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
        SELECT
            pr.Prescription_ID,
            pr.MH_ID,
            pr.Prescription_Date,
            pr.Notes,
            p.P_Name AS PatientName,
            d.D_Name AS DoctorName,
            a.Appointment_Date,
            mhd.Diagnosis_Details
        FROM Prescription pr
        INNER JOIN Medical_History mh
            ON pr.MH_ID = mh.MH_ID
        INNER JOIN Appointment a
            ON mh.Appointment_ID = a.Appointment_ID
        INNER JOIN Patient p
            ON a.Patient_ID = p.Patient_ID
        INNER JOIN Doctor d
            ON a.Doctor_ID = d.Doctor_ID
        LEFT JOIN Medical_History_Detail mhd
            ON mh.MH_ID = mhd.MH_ID
        ORDER BY pr.Prescription_Date DESC, pr.Prescription_ID DESC;
        """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        prescriptions.Add(new Prescription
        {
            PrescriptionId = reader.GetInt32("Prescription_ID"),
            MedicalHistoryId = reader.GetInt32("MH_ID"),
            PrescriptionDate = reader.IsDBNull(
                reader.GetOrdinal("Prescription_Date"))
                ? null
                : reader.GetDateTime("Prescription_Date"),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString("Notes"),
            PatientName = reader.GetString("PatientName"),
            DoctorName = reader.GetString("DoctorName"),
            AppointmentDate = reader.GetDateTime("Appointment_Date"),
            DiagnosisDetails = reader.IsDBNull(
                reader.GetOrdinal("Diagnosis_Details"))
                ? null
                : reader.GetString("Diagnosis_Details")
        });
    }

    return prescriptions;
}

public async Task<List<PrescriptionItem>> GetPrescriptionItemsAsync(
    int prescriptionId)
{
    var items = new List<PrescriptionItem>();

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       SELECT
                           pi.Item_ID,
                           pi.Prescription_ID,
                           pi.Medicine_ID,
                           m.Name AS MedicineName,
                           pi.Quantity,
                           pi.Dosage,
                           pi.Frequency,
                           pi.Duration,
                           pi.Instructions
                       FROM Prescription_Item pi
                       INNER JOIN Medicine m
                           ON pi.Medicine_ID = m.Medicine_ID
                       WHERE pi.Prescription_ID = @PrescriptionId
                       ORDER BY pi.Item_ID;
                       """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@PrescriptionId", prescriptionId);

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        items.Add(new PrescriptionItem
        {
            ItemId = reader.GetInt32("Item_ID"),
            PrescriptionId = reader.GetInt32("Prescription_ID"),
            MedicineId = reader.GetInt32("Medicine_ID"),
            MedicineName = reader.GetString("MedicineName"),
            Quantity = reader.GetInt32("Quantity"),
            Dosage = reader.IsDBNull(reader.GetOrdinal("Dosage"))
                ? null
                : reader.GetString("Dosage"),
            Frequency = reader.IsDBNull(reader.GetOrdinal("Frequency"))
                ? null
                : reader.GetString("Frequency"),
            Duration = reader.IsDBNull(reader.GetOrdinal("Duration"))
                ? null
                : reader.GetString("Duration"),
            Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions"))
                ? null
                : reader.GetString("Instructions")
        });
    }

    return items;
}


public async Task AddPrescriptionItemAsync(PrescriptionItemInput input)
{
    if (input.Quantity <= 0)
    {
        throw new InvalidOperationException(
            "Prescription quantity must be greater than zero.");
    }

    if (input.PrescriptionId <= 0)
    {
        throw new InvalidOperationException(
            "Please select a valid prescription.");
    }

    if (input.MedicineId <= 0)
    {
        throw new InvalidOperationException(
            "Please select a valid medicine.");
    }
    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    await using var transaction =
        await connection.BeginTransactionAsync();

    try
    {
        // Deduct stock only if enough units are available.
        const string stockSql = """
            UPDATE Medicine
            SET Stock_Quantity = Stock_Quantity - @Quantity
            WHERE Medicine_ID = @MedicineId
              AND Stock_Quantity >= @Quantity;
            """;

        await using (var stockCommand =
            new MySqlCommand(stockSql, connection, transaction))
        {
            stockCommand.Parameters.AddWithValue(
                "@Quantity", input.Quantity);

            stockCommand.Parameters.AddWithValue(
                "@MedicineId", input.MedicineId);

            int rowsAffected = await stockCommand.ExecuteNonQueryAsync();

            if (rowsAffected != 1)
            {
                throw new InvalidOperationException(
                    "Insufficient stock or the selected medicine does not exist.");
            }
        }
        
        // Prevent the same medicine from being added twice
// to the same prescription.
        const string duplicateSql = """
                                    SELECT COUNT(*)
                                    FROM Prescription_Item
                                    WHERE Prescription_ID = @PrescriptionId
                                      AND Medicine_ID = @MedicineId;
                                    """;

        await using (var duplicateCommand =
                     new MySqlCommand(duplicateSql, connection, transaction))
        {
            duplicateCommand.Parameters.AddWithValue(
                "@PrescriptionId", input.PrescriptionId);

            duplicateCommand.Parameters.AddWithValue(
                "@MedicineId", input.MedicineId);

            var duplicateCount =
                Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync());

            if (duplicateCount > 0)
            {
                throw new InvalidOperationException(
                    "This medicine has already been added to this prescription.");
            }
        }

        // Insert the prescription item.
        const string insertSql = """
            INSERT INTO Prescription_Item
                (Prescription_ID, Medicine_ID, Quantity,
                 Dosage, Frequency, Duration, Instructions)
            VALUES
                (@PrescriptionId, @MedicineId, @Quantity,
                 @Dosage, @Frequency, @Duration, @Instructions);
            """;

        await using (var insertCommand =
            new MySqlCommand(insertSql, connection, transaction))
        {
            insertCommand.Parameters.AddWithValue(
                "@PrescriptionId", input.PrescriptionId);

            insertCommand.Parameters.AddWithValue(
                "@MedicineId", input.MedicineId);

            insertCommand.Parameters.AddWithValue(
                "@Quantity", input.Quantity);

            insertCommand.Parameters.AddWithValue(
                "@Dosage", (object?)input.Dosage ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "@Frequency", (object?)input.Frequency ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "@Duration", (object?)input.Duration ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "@Instructions", (object?)input.Instructions ?? DBNull.Value);

            await insertCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}


public async Task RestockMedicineAsync(int medicineId, int quantityToAdd)
{
    if (medicineId <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(medicineId));
    }

    if (quantityToAdd <= 0)
    {
        throw new ArgumentOutOfRangeException(
            nameof(quantityToAdd),
            "Restock quantity must be greater than zero.");
    }

    await using var connection =
        new MySqlConnection(_connectionString);

    await connection.OpenAsync();

    const string sql = """
                       UPDATE Medicine
                       SET Stock_Quantity = Stock_Quantity + @Quantity
                       WHERE Medicine_ID = @MedicineId
                         AND Stock_Quantity <= 2147483647 - @Quantity;
                       """;

    await using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@Quantity", quantityToAdd);
    command.Parameters.AddWithValue("@MedicineId", medicineId);

    int rowsAffected = await command.ExecuteNonQueryAsync();

    if (rowsAffected != 1)
    {
        throw new InvalidOperationException(
            "The medicine was not found or the resulting stock quantity is too large.");
    }
}


}

public class Patient
{
    public int PatientId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string Phone { get; set; } = "";
}

public class Doctor
{
    public int DoctorId { get; set; }
    public string Name { get; set; } = "";
    public string Specialization { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

public class DoctorInput
{
    public string Name { get; set; } = "";
    public string Specialization { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

public class Appointment
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan AppointmentTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string Reason { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string DoctorName { get; set; } = "";
}

public class AppointmentInput
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan AppointmentTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Reason { get; set; }
}

public class MedicalHistory
{
    public int MedicalHistoryId { get; set; }
    public int AppointmentId { get; set; }
    public DateTime? MedicalHistoryDate { get; set; }
    public string? Notes { get; set; }

    public string PatientName { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public DateTime AppointmentDate { get; set; }

    public string? DiagnosisDetails { get; set; }
    public string? TreatmentDetails { get; set; }
}

public class MedicalHistoryInput
{
    public int AppointmentId { get; set; }
    public DateTime MedicalHistoryDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    public string? DiagnosisDetails { get; set; }
    public string? TreatmentDetails { get; set; }
}

public class Medicine
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Manufacturer { get; set; }
    public string? Unit { get; set; }
    public int StockQuantity { get; set; }
}

public class MedicineInput
{
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Manufacturer { get; set; }
    public string? Unit { get; set; }
    public int StockQuantity { get; set; }
}

public class Prescription
{
    public int PrescriptionId { get; set; }
    public int MedicalHistoryId { get; set; }
    public DateTime? PrescriptionDate { get; set; }
    public string? Notes { get; set; }

    // Display details for the Prescription Management page.
    public string PatientName { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public DateTime AppointmentDate { get; set; }
    public string? DiagnosisDetails { get; set; }
}

public class PrescriptionItem
{
    public int ItemId { get; set; }
    public int PrescriptionId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = "";
    public int Quantity { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

public class PrescriptionInput
{
    public int MedicalHistoryId { get; set; }
    public DateTime PrescriptionDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
}
public class PrescriptionItemInput
{
    public int PrescriptionId { get; set; }
    public int MedicineId { get; set; }
    public int Quantity { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}