// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Data.SQLite;
using System.IO;

namespace Zakfo.Unphased.Features.planner {
    internal static class TargetCatalog {
        internal static string FindType(FramingTarget target) {
            // Framing Assistant can recreate the object from just its name and coordinates.
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "NINA.sqlite");
            if (!File.Exists(path)) return null;
            var options = new SQLiteConnectionStringBuilder {
                DataSource = path, ReadOnly = true, FailIfMissing = true
            };
            using var connection = new SQLiteConnection(options.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT d.ra, d.dec, d.dsotype
                FROM dsodetail AS d
                WHERE d.dec BETWEEN @minDec AND @maxDec
                  AND d.ra IS NOT NULL AND d.dsotype IS NOT NULL
                  AND (REPLACE(UPPER(d.id), ' ', '') IN (@name, @id)
                       OR EXISTS (SELECT 1 FROM cataloguenr AS c
                                  WHERE c.dsodetailid = d.id
                                  AND REPLACE(UPPER(c.catalogue || c.designation), ' ', '') IN (@name, @id)))
                """;
            const double arcminute = 1.0 / 60;
            command.Parameters.AddWithValue("@minDec", target.DeclinationDegrees - arcminute);
            command.Parameters.AddWithValue("@maxDec", target.DeclinationDegrees + arcminute);
            command.Parameters.AddWithValue("@name", CatalogKey(target.Name));
            command.Parameters.AddWithValue("@id", CatalogKey(target.Id));
            using var rows = command.ExecuteReader();
            string type = null;
            while (rows.Read()) {
                var candidate = rows.GetString(2);
                if (string.IsNullOrWhiteSpace(candidate) || !AtCatalogPosition(target, rows.GetDouble(0), rows.GetDouble(1))) continue;
                if (type != null && !type.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return null;
                type = candidate;
            }
            return type;
        }

        private static bool AtCatalogPosition(FramingTarget target, double raDegrees, double decDegrees) {
            // Both positions are J2000. An exact name alone can describe a custom framing centre.
            const double radians = Math.PI / 180;
            var declination = target.DeclinationDegrees * radians;
            var catalogDeclination = decDegrees * radians;
            var raDifference = (target.RightAscensionHours * 15 - raDegrees) * radians;
            var cosine = Math.Sin(declination) * Math.Sin(catalogDeclination)
                + Math.Cos(declination) * Math.Cos(catalogDeclination) * Math.Cos(raDifference);
            return cosine >= Math.Cos(radians / 60);
        }

        private static string CatalogKey(string name) => name?.Trim().Replace(" ", "").ToUpperInvariant();
    }
}
