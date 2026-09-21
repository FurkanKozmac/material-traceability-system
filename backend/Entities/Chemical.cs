namespace backend.Entities;

public class Chemical
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ChemicalCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MsdsUrl { get; set; }
    public bool Stopped { get; set; } = false;
    public string StorageType { get; set; } = "GENERAL";
    public ICollection<Batch> Batches { get; set; } = [];
}
