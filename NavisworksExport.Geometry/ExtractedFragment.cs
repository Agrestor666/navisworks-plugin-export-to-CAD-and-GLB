using System.Collections.Generic;

namespace NavisworksExport.Geometry
{
    /// <summary>
    /// World-space colored triangles for one fragment occurrence after dedup/transform.
    /// Format-agnostic — no glTF/DWG types and no BIM property payload.
    /// </summary>
    public sealed class ExtractedFragment
    {
        public ExtractedFragment(IReadOnlyList<ExtractedTriangle> triangles)
        {
            Triangles = triangles;
        }

        public IReadOnlyList<ExtractedTriangle> Triangles { get; }
    }
}
