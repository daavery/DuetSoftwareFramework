﻿using DuetAPI.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DuetControlServer
{
    /// <summary>
    /// Settings provider
    /// </summary>
    /// <remarks>This class cannot be static because JSON.NET requires an instance for deserialization</remarks>
    public static class Settings
    {
        private const string DefaultConfigFile = "/opt/dsf/conf/config.json";
        private const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.Singleline;

        /// <summary>
        /// Path to the UNIX socket for IPC
        /// </summary>
        /// <seealso cref="DuetAPI"/>
        public static string SocketPath { get; set; } = DuetAPI.Connection.Defaults.SocketPath;

        /// <summary>
        /// Maximum number of pending IPC connection
        /// </summary>
        public static int Backlog { get; set; } = 4;

        /// <summary>
        /// Virtual SD card directory.
        /// Paths starting with 0:/ are mapped to this directory
        /// </summary>
        public static string BaseDirectory { get; set; } = "/opt/dsf/sd";

        /// <summary>
        /// Internal model update interval after which properties of the machine model from
        /// the host controller (e.g. network information and mass storages) are updated (in ms)
        /// </summary>
        public static int HostUpdateInterval { get; set; } = 4000;

        /// <summary>
        /// How frequently the config response is polled (in ms; temporary; will be removed once the new object model has been finished)
        /// </summary>
        public static int ConfigUpdateInterval { get; set; } = 5000;

        /// <summary>
        /// Maximum time to keep messages in the object model unless client(s) pick them up (in s).
        /// Note that messages are only cleared when the host update task runs.
        /// </summary>
        public static double MaxMessageAge { get; set; } = 60.0;

        /// <summary>
        /// SPI device that is connected to RepRapFirmware
        /// </summary>
        public static string SpiDevice { get; set; } = "/dev/spidev0.0";

        /// <summary>
        /// Frequency to use for SPI transfers
        /// </summary>
        public static int SpiFrequency { get; set; } = 2_000_000;

        /// <summary>
        /// Maximum allowed delay between data exchanges during a full transfer (in ms)
        /// </summary>
        public static int SpiTransferTimeout { get; set; } = 500;

        /// <summary>
        /// Maximum number of sequential transfer retries
        /// </summary>
        public static int MaxSpiRetries { get; set; } = 3;

        /// <summary>
        /// Time to wait after every full transfer (in ms)
        /// </summary>
        public static int SpiPollDelay { get; set; } = 25;

        /// <summary>
        /// Path to the GPIO chip device node
        /// </summary>
        public static string GpioChipDevice { get; set; } = "/dev/gpiochip0";

        /// <summary>
        /// Number of the GPIO pin that is used by RepRapFirmware to flag its ready state
        /// </summary>
        public static int TransferReadyPin { get; set; } = 25;      // Pin 22 on the RaspPi expansion header

        /// <summary>
        /// Number of codes to buffer in the internal print subsystem
        /// </summary>
        public static int BufferedPrintCodes { get; set; } = 32;

        /// <summary>
        /// Number of codes to buffer per macro
        /// </summary>
        public static int BufferedMacroCodes { get; set; } = 16;

        /// <summary>
        /// Maximum space of buffered codes per channel (in bytes). Must be greater than <see cref="SPI.Communication.Consts.MaxCodeBufferSize"/>
        /// </summary>
        public static int MaxBufferSpacePerChannel { get; set; } = 1536;

        /// <summary>
        /// Interval of regular status updates (in ms)
        /// </summary>
        /// <remarks>This is preliminary and will be removed from future versions</remarks>
        public static double ModelUpdateInterval { get; set; } = 125.0;

        /// <summary>
        /// How many bytes to parse max at the beginning of a file to retrieve G-code file information (12KiB)
        /// </summary>
        public static uint FileInfoReadLimitHeader { get; set; } = 12288;

        /// <summary>
        /// How many bytes to parse max at the end of a file to retrieve G-code file information (256KiB)
        /// </summary>
        public static uint FileInfoReadLimitFooter { get; set; } = 262144;

        /// <summary>
        /// Maximum allowed layer height. Used by the file info parser
        /// </summary>
        public static double MaxLayerHeight { get; set; } = 0.9;

        /// <summary>
        /// Regular expressions for finding the layer height (case insensitive)
        /// </summary>
        public static List<Regex> LayerHeightFilters { get; set; } = new List<Regex>
        {
            new Regex(@"layer_height\D+(?<mm>(\d+\.?\d*))", RegexFlags),                // Slic3r
            new Regex(@"Layer height\D+(?<mm>(\d+\.?\d*))", RegexFlags),                // Cura
            new Regex(@"layerHeight\D+(?<mm>(\d+\.?\d*))", RegexFlags),                 // Simplify3D
            new Regex(@"layer_thickness_mm\D+(?<mm>(\d+\.?\d*))", RegexFlags),          // KISSlicer and Canvas
            new Regex(@"layerThickness\D+(?<mm>(\d+\.?\d*))", RegexFlags)               // Matter Control
        };

        /// <summary>
        /// Regular expressions for finding the filament consumption (case insensitive, single line)
        /// </summary>
        public static List<Regex> FilamentFilters { get; set; } = new List<Regex>
        {
            new Regex(@"filament used\D+(((?<mm>\d+\.?\d*)mm)(\D+)?)+", RegexFlags),        // Slic3r (mm)
            new Regex(@"filament used\D+(((?<m>\d+\.?\d*)m([^m]|$))(\D+)?)+", RegexFlags),  // Cura (m)
            new Regex(@"material\#\d+\D+(?<mm>\d+\.?\d*)", RegexFlags),                     // IdeaMaker (mm)
            new Regex(@"filament length\D+(((?<mm>\d+\.?\d*)\s*mm)(\D+)?)+", RegexFlags),   // Simplify3D (mm)
            new Regex(@"Filament used per extruder:\r\n;\s*(?<name>.+)\s+=\s*(?<mm>[0-9.]+)", RegexFlags)   // Canvas
        };

        /// <summary>
        /// Regular expressions for finding the slicer (case insensitive)
        /// </summary>
        public static List<Regex> GeneratedByFilters { get; set; } = new List<Regex>
        {
            new Regex(@"generated by\s+(.+)", RegexFlags),                              // Slic3r and Simplify3D
            new Regex(@";\s*Sliced by\s+(.+)", RegexFlags),                             // IdeaMaker and Canvas
            new Regex(@";\s*(KISSlicer.*)", RegexFlags),                                // KISSlicer
            new Regex(@";\s*Sliced at:\s*(.+)", RegexFlags),                            // Cura (old)
            new Regex(@";\s*Generated with\s*(.+)", RegexFlags)                         // Cura (new)
        };

        /// <summary>
        /// Regular expressions for finding the print time
        /// </summary>
        public static List<Regex> PrintTimeFilters { get; set; } = new List<Regex>
        {
            new Regex(@"estimated printing time = ((?<h>(\d+))h\s*)?((?<m>(\d+))m\s*)?((?<s>(\d+))s)?", RegexFlags),                                     // Slic3r PE
            new Regex(@";TIME:(?<s>(\d+\.?\d*))", RegexFlags),                                                                                           // Cura
            new Regex(@"Build time: ((?<h>\d+) hours\s*)?((?<m>\d+) minutes\s*)?((?<s>(\d+) seconds))?", RegexFlags),                                    // Simplify3D
            new Regex(@"Estimated Build Time:\s+((?<h>(\d+\.?\d*)) hours\s*)?((?<m>(\d+\.?\d*)) minutes\s*)?((?<s>(\d+\.?\d*)) seconds)?", RegexFlags)   // KISSlicer and Canvas
        };

        /// <summary>
        /// Regular expressions for finding the simulated time
        /// </summary>
        public static List<Regex> SimulatedTimeFilters { get; set; } = new List<Regex>
        {
            new Regex(@"; Simulated print time\D+(?<s>(\d+\.?\d*))", RegexFlags)
        };

        /// <summary>
        /// Initialize settings and load them from the config file or create it if it does not exist
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        internal static void Init(string[] args)
        {
            // Attempt to parse the config file path from the command-line arguments
            string lastArg = null, config = DefaultConfigFile;
            foreach (string arg in args)
            {
                if (lastArg == "-s" || lastArg == "--config")
                {
                    config = arg;
                    break;
                }
                lastArg = arg;
            }

            // See if the file exists and attempt to load the settings from it, otherwise create it
            if (File.Exists(config))
            {
                LoadFromFile(config);

                if (MaxBufferSpacePerChannel < SPI.Communication.Consts.MaxCodeBufferSize)
                {
                    throw new ArgumentException($"{nameof(MaxBufferSpacePerChannel)} is too low");
                }
            }
            else
            {
                SaveToFile(config);
            }
            
            // Parse other command-line parameters
            ParseParameters(args);
        }

        private static void ParseParameters(string[] args)
        {
            string lastArg = null;
            foreach (string arg in args)
            {
                if (arg == "-i" || arg == "--info")
                {
                    Console.WriteLine("-i, --info: Display this reference");
                    Console.WriteLine("-s, --config: Set config file");
                    Console.WriteLine("-S, --socket: Specify the UNIX socket path");
                    Console.WriteLine("-b, --base-directory: Set the base path for system and G-code files");
                    Console.WriteLine("-u, --update: Update RepRapFirmware and exit");
                }
                else if (lastArg == "-S" || lastArg == "--socket")
                {
                    SocketPath = arg;
                }
                else if (lastArg == "-b" || lastArg == "--base-directory")
                {
                    BaseDirectory = arg;
                }
                lastArg = arg;
            }
        }

        private static void LoadFromFile(string fileName)
        {
            byte[] content;
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                content = new byte[fileStream.Length];
                fileStream.Read(content, 0, (int)fileStream.Length);
            }

            Utf8JsonReader reader = new Utf8JsonReader(content);
            PropertyInfo property = null;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        string propertyName = reader.GetString();
                        property = typeof(Settings).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                        if (property == null || Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                        {
                            // Skip non-existent and ignored properties
                            if (reader.Read())
                            {
                                if (reader.TokenType == JsonTokenType.StartArray)
                                {
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
                                }
                                else if (reader.TokenType == JsonTokenType.StartObject)
                                {
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) { }
                                }
                            }
                        }
                        break;

                    case JsonTokenType.Number:
                        if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(null, reader.GetInt32());
                        }
                        else if (property.PropertyType == typeof(uint))
                        {
                            property.SetValue(null, reader.GetUInt32());
                        }
                        else if (property.PropertyType == typeof(float))
                        {
                            property.SetValue(null, reader.GetSingle());
                        }
                        else if (property.PropertyType == typeof(double))
                        {
                            property.SetValue(null, reader.GetDouble());
                        }
                        else
                        {
                            throw new JsonException($"Bad number type: {property.PropertyType.Name}");
                        }
                        break;

                    case JsonTokenType.String:
                        if (property.PropertyType == typeof(string))
                        {
                            property.SetValue(null, reader.GetString());
                        }
                        else
                        {
                            throw new JsonException($"Bad string type: {property.PropertyType.Name}");
                        }
                        break;

                    case JsonTokenType.StartArray:
                        if (property.PropertyType == typeof(List<Regex>))
                        {
                            JsonRegexListConverter regexListConverter = new JsonRegexListConverter();
                            property.SetValue(null, regexListConverter.Read(ref reader, typeof(List<Regex>), null));
                        }
                        else
                        {
                            throw new JsonException($"Bad list type: {property.PropertyType.Name}");
                        }
                        break;
                }
            }
        }

        private static void SaveToFile(string fileName)
        {
            using FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using Utf8JsonWriter writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions() { Indented = true });

            writer.WriteStartObject();
            foreach (PropertyInfo property in typeof(Settings).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                object value = property.GetValue(null);
                if (value is string stringValue)
                {
                    writer.WriteString(property.Name, stringValue);
                }
                else if (value is int intValue)
                {
                    writer.WriteNumber(property.Name, intValue);
                }
                else if (value is uint uintValue)
                {
                    writer.WriteNumber(property.Name, uintValue);
                }
                else if (value is float floatValue)
                {
                    writer.WriteNumber(property.Name, floatValue);
                }
                else if (value is double doubleValue)
                {
                    writer.WriteNumber(property.Name, doubleValue);
                }
                else if (value is List<Regex> regexList)
                {
                    writer.WritePropertyName(property.Name);

                    JsonRegexListConverter regexListConverter = new JsonRegexListConverter();
                    regexListConverter.Write(writer, regexList, null);
                }
                else
                {
                    throw new JsonException($"Unknown value type {property.PropertyType.Name}");
                }
            }
            writer.WriteEndObject();
        }
    }
}
