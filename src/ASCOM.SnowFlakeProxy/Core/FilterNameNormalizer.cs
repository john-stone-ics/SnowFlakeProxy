using System.Text.RegularExpressions;

namespace ASCOM.SnowFlakeProxy
{
    internal static class FilterNameNormalizer
    {
        private static readonly Regex wanderer_decoration = new Regex(
            @"^Filter\s+([1-9][0-9]*)\s+\((.*)\)$",
            RegexOptions.CultureInvariant);

        internal static string Normalize(string vendor_name)
        {
            if (vendor_name == null)
            {
                return vendor_name;
            }

            string trimmed = vendor_name.Trim();
            Match match = wanderer_decoration.Match(trimmed);
            if (match.Success)
            {
                return match.Groups[2].Value;
            }

            return trimmed;
        }

        internal static string[] NormalizeAll(string[] vendor_names, bool normalize_filter_names)
        {
            if (vendor_names == null)
            {
                return new string[0];
            }

            string[] result = new string[vendor_names.Length];
            for (int index = 0; index < vendor_names.Length; index++)
            {
                if (normalize_filter_names)
                {
                    result[index] = Normalize(vendor_names[index]);
                }
                else if (vendor_names[index] == null)
                {
                    result[index] = vendor_names[index];
                }
                else
                {
                    result[index] = vendor_names[index].Trim();
                }
            }

            return result;
        }
    }
}
