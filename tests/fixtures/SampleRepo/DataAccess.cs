namespace SampleRepo;

public class DataAccess
{
    // Defect: raw string concatenation builds a WHERE clause (SQL injection vector).
    public string BuildQuery(string userName)
    {
        return "SELECT * FROM Users WHERE Name = '" + userName + "'";
    }
}
