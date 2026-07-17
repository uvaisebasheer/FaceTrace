using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.App.Services;

/// <summary>
/// Replaces MainForm.button2_Click ("Add Face"). Two bugs from the original
/// are fixed by construction here:
///   1. The caller must pass in the *exact* DetectedFace + frame it wants to
///      label — there's no shared mutable "result" field that can be stale
///      or null, so the old NullReferenceException can't happen.
///   2. Enrolling a face is an INSERT of one row, not a rewrite of every
///      .bmp file and the whole label text file.
/// </summary>
public sealed class EnrollmentService
{
    private readonly IPersonRepository _personRepository;
    private readonly IFaceEmbeddingRepository _embeddingRepository;
    private readonly IFaceEmbedder _embedder;
    private readonly IFaceMatcher _matcher;

    public EnrollmentService(
        IPersonRepository personRepository,
        IFaceEmbeddingRepository embeddingRepository,
        IFaceEmbedder embedder,
        IFaceMatcher matcher)
    {
        _personRepository = personRepository;
        _embeddingRepository = embeddingRepository;
        _embedder = embedder;
        _matcher = matcher;
    }

    /// <summary>
    /// Enrolls the given face (from the given full frame) under the given
    /// person name, creating the Person record if it doesn't exist yet, and
    /// refreshes the in-memory matcher index so the new face is recognized
    /// on the very next frame.
    /// </summary>
    public async Task<Person> EnrollFaceAsync(
        string personName,
        byte[] frameBgrBytes, int frameWidth, int frameHeight,
        DetectedFace face,
        string? sourceVideoName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(personName))
            throw new ArgumentException("A name is required to enroll a face.", nameof(personName));

        var person = await _personRepository.GetByNameAsync(personName, ct)
                     ?? await _personRepository.AddAsync(personName, notes: null, ct);

        float[] embedding = _embedder.Embed(frameBgrBytes, frameWidth, frameHeight, face);
        await _embeddingRepository.AddAsync(person.Id, embedding, sourceVideoName, ct);

        // Refresh once, here — NOT per frame. This single call replaces the
        // old app's "new EigenObjectRecognizer1(...)" that used to run on
        // every detected face on every frame.
        await _matcher.RefreshIndexAsync(ct);

        return person;
    }
}
