using System.Collections.Generic;
using Colossal;

namespace TownRoadLane.Localization
{
    public class LocaleRU : IDictionarySource
    {
        private readonly TownRoadLaneSetting _setting;
        public LocaleRU(TownRoadLaneSetting setting) { _setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "Town Road Lane" },
                { _setting.GetOptionTabLocaleID(TownRoadLaneSetting.kSection), "Основное" },

                { _setting.GetOptionGroupLocaleID(TownRoadLaneSetting.kEdgeGroup), "Краевая линия у бордюра" },
                { _setting.GetOptionGroupLocaleID(TownRoadLaneSetting.kParkingGroup), "Разметка параллельной парковки" },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.EdgeLineEnabled)), "Краевая линия на городских дорогах" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.EdgeLineEnabled)),
                    "Добавляет краевую линию у бордюра обычным городским дорогам (полосы 3 м) — так же, как на шоссе. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.EdgeLineStyle)), "Стиль краевой линии" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.EdgeLineStyle)),
                    "Стиль меша автоматической краевой линии. Все чёткие и ванильные стили встроены без внешних зависимостей. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.YellowLeftLineEnabled)), "Жёлтая левая краевая (США)" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.YellowLeftLineEnabled)),
                    "В городах с североамериканской темой односторонние и разделённые дороги получают жёлтую линию вдоль левого края проезжей части (со стороны медианы), как принято в США; белая краевая остаётся у бордюра. Города с европейской темой не затрагиваются. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingMarkingsEnabled)), "Размечать зоны параллельной парковки" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingMarkingsEnabled)),
                    "Рисует линию вдоль зон параллельной уличной парковки с поперечной чертой на концах квартала. Дороги без сублейна Parking Lane 2 (односторонние трёхполосные, асимметричные варианты) остаются без разметки — то же покрытие, что и в v1.1. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingLineStyle)), "Стиль линии парковки" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingLineStyle)),
                    "Продольная линия вдоль парковочной зоны. Все стили встроены в мод. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(TownRoadLaneSetting.ParkingEndStyle)), "Стиль концевой черты парковки" },
                { _setting.GetOptionDescLocaleID(nameof(TownRoadLaneSetting.ParkingEndStyle)),
                    "Короткая поперечная черта в начале и конце парковочного квартала. «Нет» отключает черты. Все стили встроены в мод. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolidThick), "Белая сплошная (толстая)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteDashed), "Белый пунктир" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.YellowSolid), "Жёлтая сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteSolid_G87), "Белая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.WhiteDashed_G87), "Белый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.EdgeLineStyleEnum.YellowSolid_G87), "Жёлтая сплошная (чёткая)" },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashedDense), "Белый пунктир (частый)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashed), "Белый пунктир" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowDashed), "Жёлтый пунктир" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowSolid), "Жёлтая сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteSolid_G87), "Белая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.WhiteDashed_G87), "Белый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowSolid_G87), "Жёлтая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.YellowDashed_G87), "Жёлтый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.BlueSolid_G87), "Синяя сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingLineStyleEnum.BlueDashed_G87), "Синий пунктир" },

                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.None), "Нет" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteSolidThick), "Белая сплошная (толстая)" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.WhiteTerminal_G87), "Белая концевая линия" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.YellowTerminal_G87), "Жёлтая концевая линия" },
                { _setting.GetEnumValueLocaleID(TownRoadLaneSetting.ParkingEndStyleEnum.BlueSolid_G87), "Синяя сплошная" },
            };
        }

        public void Unload() { }
    }
}
