using System.Collections.Generic;
using Colossal;

namespace AutoEdgeMarkings.Localization
{
    public class LocaleRU : IDictionarySource
    {
        private readonly AutoEdgeMarkingsSetting _setting;
        public LocaleRU(AutoEdgeMarkingsSetting setting) { _setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "Auto Edge Markings" },
                { _setting.GetOptionTabLocaleID(AutoEdgeMarkingsSetting.kSection), "Основное" },

                { _setting.GetOptionGroupLocaleID(AutoEdgeMarkingsSetting.kEdgeGroup), "Краевая линия у бордюра" },
                { _setting.GetOptionGroupLocaleID(AutoEdgeMarkingsSetting.kParkingGroup), "Разметка параллельной парковки" },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineEnabled)), "Краевая линия на городских дорогах" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineEnabled)),
                    "Добавляет краевую линию у бордюра обычным городским дорогам (полосы 3 м) — так же, как на шоссе. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineStyle)), "Стиль краевой линии" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.EdgeLineStyle)),
                    "Стиль меша автоматической краевой линии. Все чёткие и ванильные стили встроены без внешних зависимостей. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.YellowLeftLineEnabled)), "Жёлтая левая краевая (США)" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.YellowLeftLineEnabled)),
                    "В городах с североамериканской темой односторонние и разделённые дороги получают жёлтую линию вдоль левого края проезжей части (со стороны медианы), как принято в США; белая краевая остаётся у бордюра. Города с европейской темой не затрагиваются. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingMarkingsEnabled)), "Размечать зоны параллельной парковки" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingMarkingsEnabled)),
                    "Рисует линию вдоль зон параллельной уличной парковки с поперечной чертой на концах квартала. Дороги без сублейна Parking Lane 2 (односторонние трёхполосные, асимметричные варианты) остаются без разметки. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingLineStyle)), "Стиль линии парковки" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingLineStyle)),
                    "Продольная линия вдоль парковочной зоны. Все стили встроены в мод. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetOptionLabelLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingEndStyle)), "Стиль концевой черты парковки" },
                { _setting.GetOptionDescLocaleID(nameof(AutoEdgeMarkingsSetting.ParkingEndStyle)),
                    "Короткая поперечная черта в начале и конце парковочного квартала. «Нет» отключает черты. Все стили встроены в мод. Изменения вступают в силу после перезапуска игры." },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolidThick), "Белая сплошная (толстая)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteDashed), "Белый пунктир" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.YellowSolid), "Жёлтая сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteSolid_G87), "Белая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.WhiteDashed_G87), "Белый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.EdgeLineStyleEnum.YellowSolid_G87), "Жёлтая сплошная (чёткая)" },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.WhiteDashedDense), "Белый пунктир (частый)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.WhiteDashed), "Белый пунктир" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.YellowDashed), "Жёлтый пунктир" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.YellowSolid), "Жёлтая сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.WhiteSolid_G87), "Белая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.WhiteDashed_G87), "Белый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.YellowSolid_G87), "Жёлтая сплошная (чёткая)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.YellowDashed_G87), "Жёлтый пунктир (чёткий)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.BlueSolid_G87), "Синяя сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingLineStyleEnum.BlueDashed_G87), "Синий пунктир" },

                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.None), "Нет" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.WhiteSolid), "Белая сплошная" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.WhiteSolidThick), "Белая сплошная (толстая)" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.WhiteTerminal_G87), "Белая концевая линия" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.YellowTerminal_G87), "Жёлтая концевая линия" },
                { _setting.GetEnumValueLocaleID(AutoEdgeMarkingsSetting.ParkingEndStyleEnum.BlueSolid_G87), "Синяя сплошная" },
            };
        }

        public void Unload() { }
    }
}
