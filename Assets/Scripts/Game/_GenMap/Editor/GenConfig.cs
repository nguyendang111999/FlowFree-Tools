namespace FlowFree.GenMap
{
    // Generation parameters. Full-fill maps: sum of path lengths must equal Width*Height.
    public class GenConfig
    {
        public int Width = 5;
        public int Height = 5;
        public int ColorCount = 3;
        public int MapCount = 5;
        public int MinLength = 2;
        public int MaxLength = 25;
        public int Seed = 0;
        public int PerSolveTimeoutMs = 3000;

        // When true, accepted maps do not constrain future solves (faster, allows duplicates across a batch).
        public bool ResetSolverPerMap = true;

        /// <summary>
        /// Checks the configuration is feasible for a full-fill board and reports the first problem found.
        /// </summary>
        /// <param name="error">Human-readable reason when the config is invalid; null when valid.</param>
        /// <returns>True when the settings can produce a valid board.</returns>
        public bool Validate(out string error)
        {
            error = null;
            if (Width < 2 || Height < 2) { error = "Width and Height must be >= 2."; return false; }
            if (ColorCount < 1) { error = "Color count must be >= 1."; return false; }
            if (MapCount < 1) { error = "Map count must be >= 1."; return false; }
            if (MinLength < 2) { error = "Min length must be >= 2 (a path needs two endpoints)."; return false; }
            if (MaxLength < MinLength) { error = "Max length must be >= min length."; return false; }

            int cells = Width * Height;
            if (ColorCount * MinLength > cells)
            {
                error = $"Infeasible: colors*minLen ({ColorCount * MinLength}) > cells ({cells}).";
                return false;
            }
            if (ColorCount * MaxLength < cells)
            {
                error = $"Infeasible: colors*maxLen ({ColorCount * MaxLength}) < cells ({cells}); board cannot be fully filled.";
                return false;
            }
            if (PerSolveTimeoutMs < 100) { error = "Per-solve timeout must be >= 100 ms."; return false; }
            return true;
        }
    }
}
