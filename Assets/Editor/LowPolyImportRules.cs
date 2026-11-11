using UnityEditor;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Flat-shaded low-poly look: models placed under <see cref="ModelsFolder"/> are
    /// imported with recalculated normals at a 0 degree smoothing angle, so every face
    /// gets its own hard-edged normal. See docs/adr/0002-urp-flat-shading-via-import-normals.md.
    /// </summary>
    public static class LowPolyImportRules
    {
        public const string ModelsFolder = "Assets/Art/Models/";
        public const float NormalSmoothingAngle = 0f;

        public static bool ShouldFlatShade(string assetPath) =>
            assetPath != null && assetPath.Replace('\\', '/').StartsWith(ModelsFolder, System.StringComparison.Ordinal);
    }

    internal sealed class LowPolyModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!LowPolyImportRules.ShouldFlatShade(assetPath)) return;

            var importer = (ModelImporter)assetImporter;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
            importer.normalSmoothingAngle = LowPolyImportRules.NormalSmoothingAngle;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
        }
    }
}
