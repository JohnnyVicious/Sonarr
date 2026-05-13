using System;
using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(false)]
[assembly: SuppressMessage(
    "Major Code Smell",
    "S3990: Assemblies should be marked as CLS compliant",
    Justification = "This legacy assembly exposes non-CLS public API; marking it CLS-compliant fails until that API is migrated.")]
