using LunarWard.Core.GameRecord.BH3.DailyNote;
using LunarWard.Core.GameRecord.Genshin.DailyNote;
using LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;
using LunarWard.Core.GameRecord.Genshin.SpiralAbyss;
using LunarWard.Core.GameRecord.Genshin.StygianOnslaught;
using LunarWard.Core.GameRecord.Genshin.TravelersDiary;
using LunarWard.Core.GameRecord.StarRail.ApocalypticShadow;
using LunarWard.Core.GameRecord.StarRail.ChallengePeak;
using LunarWard.Core.GameRecord.StarRail.DailyNote;
using LunarWard.Core.GameRecord.StarRail.ForgottenHall;
using LunarWard.Core.GameRecord.StarRail.PureFiction;
using LunarWard.Core.GameRecord.StarRail.SimulatedUniverse;
using LunarWard.Core.GameRecord.StarRail.TrailblazeCalendar;
using LunarWard.Core.GameRecord.ZZZ.DailyNote;
using LunarWard.Core.GameRecord.ZZZ.DeadlyAssault;
using LunarWard.Core.GameRecord.ZZZ.GachaRecord;
using LunarWard.Core.GameRecord.ZZZ.InterKnotReport;
using LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;
using LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;
using LunarWard.Core.GameRecord.ZZZ.UpgradeGuide;
using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord;


[JsonSerializable(typeof(miHoYoApiWrapper<GameRecordUserWrapper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GameRecordRoleWrapper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GameRecordIndex>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SpiralAbyssInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<StygianOnslaughtWrapper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<TravelersDiarySummary>))]
[JsonSerializable(typeof(miHoYoApiWrapper<TravelersDiaryDetail>))]
[JsonSerializable(typeof(miHoYoApiWrapper<TrailblazeCalendarSummary>))]
[JsonSerializable(typeof(miHoYoApiWrapper<TrailblazeCalendarDetail>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ForgottenHallInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<PureFictionInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ApocalypticShadowInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<SimulatedUniverseInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ChallengePeakData>))]
[JsonSerializable(typeof(miHoYoApiWrapper<DeviceFpResult>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ImaginariumTheaterWarpper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<InterKnotReportSummary>))]
[JsonSerializable(typeof(miHoYoApiWrapper<InterKnotReportDetail>))]
[JsonSerializable(typeof(miHoYoApiWrapper<UpgradeGuideItemList>))]
[JsonSerializable(typeof(miHoYoApiWrapper<UpgradeGuidIconInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<GenshinDailyNote>))]
[JsonSerializable(typeof(miHoYoApiWrapper<StarRailDailyNote>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ShiyuDefenseWrapper>))]
[JsonSerializable(typeof(miHoYoApiWrapper<DeadlyAssaultInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ZZZDailyNote>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ZZZGachaRecordData>))]
[JsonSerializable(typeof(miHoYoApiWrapper<BH3DailyNote>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ThresholdSimulationAbstractInfo>))]
[JsonSerializable(typeof(miHoYoApiWrapper<ThresholdSimulationDetailInfo>))]
[JsonSerializable(typeof(DateTimeObjectJsonConverter.DateTimeObject))]
internal partial class GameRecordJsonContext : JsonSerializerContext
{

}
