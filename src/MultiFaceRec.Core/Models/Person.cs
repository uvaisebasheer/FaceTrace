namespace MultiFaceRec.Core.Models;

/// <summary>
/// A named individual who has been enrolled into the face gallery.
/// Replaces the old "one label string per bmp" model.
/// </summary>
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Notes { get; set; }
}
