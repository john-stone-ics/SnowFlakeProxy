using System;
using System.Globalization;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    internal static class ProxySettingsStore
    {
        private const string key_trace_enabled = "trace_enabled";
        private const string key_normalize_filter_names = "normalize_filter_names";
        private const string key_move_timeout_ms = "move_timeout_ms";
        private const string key_position_retry_delay_ms = "position_retry_delay_ms";
        private const string key_connect_timeout_ms = "connect_timeout_ms";
        private const string key_setter_accept_timeout_ms = "setter_accept_timeout_ms";

        internal static ProxySettings Load(string proxy_prog_id)
        {
            ProxySettings defaults = new ProxySettings();
            ProxySettings loaded = new ProxySettings();
            using (Profile driver_profile = new Profile())
            {
                driver_profile.DeviceType = "FilterWheel";
                loaded.trace_enabled = ReadBoolean(driver_profile, proxy_prog_id, key_trace_enabled, defaults.trace_enabled);
                loaded.normalize_filter_names = ReadBoolean(driver_profile, proxy_prog_id, key_normalize_filter_names, defaults.normalize_filter_names);
                loaded.move_timeout_ms = ReadInt32(driver_profile, proxy_prog_id, key_move_timeout_ms, defaults.move_timeout_ms);
                loaded.position_retry_delay_ms = ReadInt32(driver_profile, proxy_prog_id, key_position_retry_delay_ms, defaults.position_retry_delay_ms);
                loaded.connect_timeout_ms = ReadInt32(driver_profile, proxy_prog_id, key_connect_timeout_ms, defaults.connect_timeout_ms);
                loaded.setter_accept_timeout_ms = ReadInt32(driver_profile, proxy_prog_id, key_setter_accept_timeout_ms, defaults.setter_accept_timeout_ms);
            }

            return loaded;
        }

        internal static void Save(ProxySettings settings, string proxy_prog_id)
        {
            using (Profile driver_profile = new Profile())
            {
                driver_profile.DeviceType = "FilterWheel";
                driver_profile.WriteValue(proxy_prog_id, key_trace_enabled, settings.trace_enabled.ToString());
                driver_profile.WriteValue(proxy_prog_id, key_normalize_filter_names, settings.normalize_filter_names.ToString());
                driver_profile.WriteValue(proxy_prog_id, key_move_timeout_ms, settings.move_timeout_ms.ToString(CultureInfo.InvariantCulture));
                driver_profile.WriteValue(proxy_prog_id, key_position_retry_delay_ms, settings.position_retry_delay_ms.ToString(CultureInfo.InvariantCulture));
                driver_profile.WriteValue(proxy_prog_id, key_connect_timeout_ms, settings.connect_timeout_ms.ToString(CultureInfo.InvariantCulture));
                driver_profile.WriteValue(proxy_prog_id, key_setter_accept_timeout_ms, settings.setter_accept_timeout_ms.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static bool ReadBoolean(Profile driver_profile, string proxy_prog_id, string key, bool fallback)
        {
            string raw = driver_profile.GetValue(proxy_prog_id, key, string.Empty, fallback.ToString());
            bool parsed;
            if (bool.TryParse(raw, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static int ReadInt32(Profile driver_profile, string proxy_prog_id, string key, int fallback)
        {
            string raw = driver_profile.GetValue(proxy_prog_id, key, string.Empty, fallback.ToString(CultureInfo.InvariantCulture));
            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
