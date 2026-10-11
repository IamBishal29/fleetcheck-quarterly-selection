using System.Security.Cryptography;
using System.Text;

namespace FleetCheck.Selection;

// A vehicle that could be picked. Only its Id matters for the selection.
public record Vehicle(string Id);

// The result of one quarterly selection.
// Everything here lets an auditor re-run and verify the pick later.
public record SelectionResult
{
    public string CompanyId { get; init; } = "";
    public string Quarter { get; init; } = "";
    public int Percentage { get; init; }
    public int EligibleCount { get; init; }
    public int SelectedCount { get; init; }
    public string Seed { get; init; } = "";          // derived from the inputs
    public string AlgorithmVersion { get; init; } = "";
    public List<string> SelectedVehicleIds { get; init; } = new();
}

public class QuarterlySelector
{
    public const string AlgorithmVersion = "v1";

    // Picks a share of a company's vehicles for the quarter.
    // Same inputs always give the same result, so it is reproducible and safe to run twice.
    public SelectionResult Select(
        string companyId,
        string quarter,
        List<Vehicle> activeVehicles,
        int percentage)
    {
        // 1. Check the inputs are valid.
        ValidateInputs(companyId, quarter, activeVehicles, percentage);

        // 2. Clean the list: remove blanks, remove duplicates, and sort it.
        //    Sorting means the order you pass vehicles in does not change the result.
        List<string> vehicleIds = GetCleanSortedVehicleIds(activeVehicles);

        // 3. Build the seed from the inputs.
        //    Because it comes from the inputs, an auditor can rebuild the exact same seed.
        string seed = BuildSeed(companyId, quarter, vehicleIds);

        // 4. Work out how many vehicles to pick.
        int howMany = GetHowManyToPick(vehicleIds.Count, percentage);

        // 5. Pick that many vehicles, fairly and repeatably.
        List<string> picked = PickVehicles(vehicleIds, seed, howMany);

        // 6. Return everything needed for an audit.
        return new SelectionResult
        {
            CompanyId = companyId,
            Quarter = quarter,
            Percentage = percentage,
            EligibleCount = vehicleIds.Count,
            SelectedCount = picked.Count,
            Seed = seed,
            AlgorithmVersion = AlgorithmVersion,
            SelectedVehicleIds = picked
        };
    }

    // --- Step 1: validation ---
    private void ValidateInputs(
        string companyId, string quarter, List<Vehicle> vehicles, int percentage)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            throw new ArgumentException("Company id is required.");

        if (string.IsNullOrWhiteSpace(quarter))
            throw new ArgumentException("Quarter is required.");

        if (vehicles == null)
            throw new ArgumentException("Vehicle list is required.");

        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(
                nameof(percentage), "Percentage must be between 0 and 100.");
    }

    // --- Step 2: clean and sort the vehicle ids ---
    private List<string> GetCleanSortedVehicleIds(List<Vehicle> vehicles)
    {
        var cleanIds = new List<string>();

        foreach (var vehicle in vehicles)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.Id))
                cleanIds.Add(vehicle.Id);
        }

        // Remove duplicates, then sort so the order is always the same.
        return cleanIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    // --- Step 3: build a seed from the inputs ---
    // The seed is just a hash of the company, quarter and vehicle list joined together.
    private string BuildSeed(string companyId, string quarter, List<string> vehicleIds)
    {
        string allInputs =
            AlgorithmVersion + "|" +
            companyId + "|" +
            quarter + "|" +
            string.Join(",", vehicleIds);

        return MakeHash(allInputs);
    }

    // --- Step 4: how many vehicles to pick ---
    private int GetHowManyToPick(int totalVehicles, int percentage)
    {
        if (totalVehicles == 0 || percentage == 0)
            return 0;

        // Round to the nearest whole vehicle.
        int count = (int)Math.Round(
            totalVehicles * (percentage / 100.0),
            MidpointRounding.AwayFromZero);

        // If a percentage was asked for, always pick at least one vehicle.
        if (count == 0)
            count = 1;

        // Never try to pick more than we have.
        if (count > totalVehicles)
            count = totalVehicles;

        return count;
    }

    // --- Step 5: pick the vehicles ---
    // Each vehicle gets a "score" made from the seed and its own id.
    // We sort by that score and take the first few. This gives every vehicle
    // an equal chance, and it produces the same result every time.
    private List<string> PickVehicles(List<string> vehicleIds, string seed, int howMany)
    {
        // Give each vehicle a score.
        var scoredVehicles = new List<(string Id, string Score)>();

        foreach (var id in vehicleIds)
        {
            string score = MakeHash(seed + "|" + id);
            scoredVehicles.Add((id, score));
        }

        // Sort by score (and by id if two scores are ever equal, so it stays repeatable).
        var picked = scoredVehicles
            .OrderBy(v => v.Score, StringComparer.Ordinal)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .Take(howMany)
            .Select(v => v.Id)
            .OrderBy(id => id, StringComparer.Ordinal)  // tidy, consistent output order
            .ToList();

        return picked;
    }

    // --- Helper: turn any text into a fixed hash string ---
    private string MakeHash(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
