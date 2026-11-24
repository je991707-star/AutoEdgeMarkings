using System.Collections.Generic;
using Colossal;

namespace AutoEdgeMarkings.Localization
{
    public class LocaleEN : IDictionarySource
    {
        private readonly AutoEdgeMarkingsSetting _setting;
        public LocaleEN(AutoEdgeMarkingsSetting setting) { _setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "Auto Edge Markings" },
                { _setting.GetOptionTabLocaleID(AutoEdgeMarkingsSetting.kSection), "Main" },

                { _setting.GetOptionGroupLocaleID(AutoEdgeMarkingsSetting.kEdgeGroup), "Curb-side edge line" },
                { _setting.GetOptionGroupLocaleID(AutoEdgeMarkingsSetting.kParkingGroup), "Parallel parking markings" },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineEnabled)), "Edge line on city roads" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineEnabled)),
                    "Adds the curb-side edge line to ordinary city roads (3 m car lanes), the way highway roads have it. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineStyle)), "Edge line style" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineStyle)),
                    "The mesh style used for the automatic curb-side edge line. All crisp and vanilla styles are built in with no external dependencies. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.YellowLeftLineEnabled)), "Yellow left edge line (US)" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.YellowLeftLineEnabled)),
                    "In North American-theme cities, one-way and divided roads get a yellow line along the left (median-side) edge of the carriageway, the way US roads mark it — the white edge line stays on the curb side. European-theme cities are unaffected. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingMarkingsEnabled)), "Mark parallel parking zones" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingMarkingsEnabled)),
                    "Draws a line along parallel street-parking zones with a cross tick at each end of the block. Roads without a Parking Lane 2 sublane (oneway 3-lane, asymmetric variants) remain unmarked. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingLineStyle)), "Parking line style" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingLineStyle)),
                    "The white line drawn along the parking zone: short dense dashes, or a thin continuous line. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingEndStyle)), "Parking end-tick style" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingEndStyle)),
                    "The short white perpendicular tick at the start and end of a parking block. \"None\" disables the ticks. Changes take effect after the game is restarted." },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolid), "White solid" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolidThick), "White solid (thick)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteDashed), "White dashed" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.YellowSolid), "Yellow solid" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolid_G87), "White solid (crisp)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteDashed_G87), "White dashed (crisp)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.YellowSolid_G87), "Yellow solid (crisp)" },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.Dashed), "White dashed (short)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.Solid), "White solid (thin)" },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.None), "None" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.Solid), "White solid" },
            };
        }

        public void Unload() { }
    }
}
