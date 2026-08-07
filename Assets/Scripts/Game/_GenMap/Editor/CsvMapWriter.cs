using System;
using System.IO;
using System.Text;

namespace FlowFree.GenMap
{
    // Serializes a color grid to the same CSV orientation GameData expects:
    // first line = top row (highest y), (0,0) = bottom-left.
    public static class CsvMapWriter
    {
        /// <summary>
        /// Serializes a color grid to CSV with the top row first and (0,0) at the bottom-left.
        /// </summary>
        public static string ToCsv(int[,] grid)
        {
            int cols = grid.GetLength(0);
            int rows = grid.GetLength(1);
            StringBuilder sb = new StringBuilder();
            for (int y = rows - 1; y >= 0; y--)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (x > 0) sb.Append(',');
                    sb.Append(grid[x, y]);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses CSV text back into a color grid using the same orientation ToCsv produces.
        /// </summary>
        public static int[,] Parse(string text)
        {
            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int rows = lines.Length;
            int cols = lines[0].Split(',').Length;
            int[,] grid = new int[cols, rows];
            for (int row = 0; row < rows; row++)
            {
                string[] values = lines[row].Split(',');
                int y = rows - 1 - row;
                for (int x = 0; x < cols; x++)
                {
                    grid[x, y] = int.Parse(values[x].Trim());
                }
            }
            return grid;
        }

        /// <summary>
        /// Serializes the grid, parses it back, and confirms it survives the round-trip unchanged.
        /// </summary>
        /// <param name="csv">The serialized CSV text produced from the grid.</param>
        public static bool RoundTrips(int[,] grid, out string csv)
        {
            csv = ToCsv(grid);
            int[,] parsed = Parse(csv);
            int cols = grid.GetLength(0);
            int rows = grid.GetLength(1);
            if (parsed.GetLength(0) != cols || parsed.GetLength(1) != rows) return false;
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (parsed[x, y] != grid[x, y]) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Writes CSV text to the given file path.
        /// </summary>
        public static void Write(string path, string csv)
        {
            File.WriteAllText(path, csv);
        }
    }
}
