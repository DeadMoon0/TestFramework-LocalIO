namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Bytes their creator hands over and never touches again - a file just read, text just encoded.
/// </summary>
/// <remarks>
/// <see cref="FileArtifactData"/> copies what a caller passes in, because a caller may reuse its array. Bytes
/// nobody else holds need no copy, and saying so takes this type: an array cannot be adopted by accident.
/// </remarks>
/// <param name="Bytes">The bytes, owned by nobody else.</param>
internal readonly record struct OwnedBytes(byte[] Bytes);
