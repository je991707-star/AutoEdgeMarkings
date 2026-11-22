using System.Collections.Generic;
using Colossal;

namespace TownRoadLane.Localization
{
    public class LocaleEN : IDictionarySource
    {
        private readonly TownRoadLaneSetting _setting;
        public LocaleEN(TownRoadLaneSetting setting) { _setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "Town Road Lane" },
                { _setting.GetOptionTabLocaleID(TownRoadLaneSetting.kSection), "Main" },

                { _setting.GetOptionGroupLocaleID(TownRoadLaneSetting.kEdgeGroup), "Curb-side edge line" },
                { _setting.GetOptionGroupLocaleID(TownRoadLaneSetting.kParkingGroup), "Parallel parking markings" },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.EdgeLineEnabled)), "Edge line on city roads" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.EdgeLineEnabled)),
                    "Adds the curb-side edge line to ordinary city roads (3 m car lanes), the way highway roads have it. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.EdgeLineStyle)), "Edge line style" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.EdgeLineStyle)),
                    "The mesh style used for the automatic curb-side edge line. All crisp and vanilla styles are built in with no external dependencies. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.YellowLeftLineEnabled)), "Yellow left edge line (US)" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.YellowLeftLineEnabled)),
                    "In North American-theme cities, one-way and divided roads get a yellow line along the left (median-side) edge of the carriageway, the way US roads mark it — the white edge line stays on the curb side. European-theme cities are unaffected. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingMarkingsEnabled)), "Mark parallel parking zones" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingMarkingsEnabled)),
                    "Draws a line along parallel street-parking zones with a cross tick at each end of the block. Roads without a Parking Lane 2 sublane (oneway 3-lane, asymmetric variants) remain unmarked — same coverage as v1.1. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingLineStyle)), "Parking line style" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingLineStyle)),
                    "The longitudinal line drawn along the parking zone. All styles are built in. Changes take effect after the game is restarted." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingEndStyle)), "Parking end-tick style" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingEndStyle)),
                    "The short perpendicular tick at the start and end of a parking block. \"None\" disables the ticks. All styles are built in. Changes take effect after the game is restarted." },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolid), "White solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolidThick), "White solid (thick)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteDashed), "White dashed" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.YellowSolid), "Yellow solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolid_G87), "White solid (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteDashed_G87), "White dashed (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.YellowSolid_G87), "Yellow solid (crisp)" },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashedDense), "White dashed (dense)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashed), "White dashed" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteSolid), "White solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowDashed), "Yellow dashed" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowSolid), "Yellow solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteSolid_G87), "White solid (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashed_G87), "White dashed (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowSolid_G87), "Yellow solid (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowDashed_G87), "Yellow dashed (crisp)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.BlueSolid_G87), "Blue solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.BlueDashed_G87), "Blue dashed" },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.None), "None" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteSolid), "White solid" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteSolidThick), "White solid (thick)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteTerminal_G87), "White terminal line" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.YellowTerminal_G87), "Yellow terminal line" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.BlueSolid_G87), "Blue solid" },
            };
        }

        public void Unload() { }
    }
}
