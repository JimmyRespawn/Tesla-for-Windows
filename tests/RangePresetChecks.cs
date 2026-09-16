using System;
using Newtonsoft.Json.Linq;
namespace TeslaMurphy.Services
{
 public static class RangePresetChecks
 {
  static void Check(bool value,string name){if(!value)throw new Exception(name);}
  static JObject Vehicle(string vin,string model,string trim,string wheel="PinwheelRefresh18") => JObject.FromObject(new { vin, vehicle_config=new {car_type=model,trim_badging=trim,wheel_type=wheel,charge_port_type="US",eu_vehicle=false,rhd=false}});
  public static string Run(string catalog, string chinaCatalog, string sampleJson)
  {
   var rows=(JArray)JObject.Parse(catalog)["vehicles"];
   Check(rows.Count==185,"catalog count");
   var us=Vehicle("5YJ3E1EA0MF000001","model3","50");
   var result=RangePresetService.Find(us,rows);
   Check(Math.Abs(result.Kilometers.Value-263*1.609344)<0.001,"2021 Model 3 SR+");
   Check(result.SourceUrl.EndsWith("43821"),"source attribution");
   var china=Vehicle("LRW3E7FA0MC000001","model3","50");
   china["vehicle_config"]["charge_port_type"]="GB";
   Check(!RangePresetService.Find(china,rows).Kilometers.HasValue,"no US preset for China");
   Check(!RangePresetService.Find(Vehicle("5YJ3E1EA0MF000001","model3","unknown"),rows).Kilometers.HasValue,"unknown badge");
   Check(!RangePresetService.Find(Vehicle("5YJ3E1EA0MF000001","model3","50",""),rows).Kilometers.HasValue,"missing wheel");
   Check(!RangePresetService.Find(Vehicle("5YJ3E1EA0MF000001","modely","50"),rows).Kilometers.HasValue,"VIN model mismatch");
   var duplicate=(JArray)rows.DeepClone();duplicate.Add(rows[0].DeepClone());
   foreach(var row in rows) if((string)row["epaId"]=="43821") {duplicate.Add(row.DeepClone());break;}
   Check(!RangePresetService.Find(us,duplicate).Kilometers.HasValue,"ambiguous match");
   Check(!RangePresetService.Year("invalid").HasValue,"invalid VIN");
   Check(RangePresetService.Year("5YJ3E1EA0TF000001")==2026,"2026 VIN");
   Check(RangePresetService.Find(Vehicle("5YJSA1E20JF000001","models","75d","Slipstream19"),rows).Kilometers.HasValue,"legacy S badge");
   var noMarket=(JObject)us.DeepClone();((JObject)noMarket["vehicle_config"]).Remove("eu_vehicle");
   Check(!RangePresetService.Find(noMarket,rows).Kilometers.HasValue,"missing region");
   foreach(JObject row in rows) Check((double)row["rangeMiles"]>0 && (int)row["year"]>=2012 && !string.IsNullOrEmpty((string)row["epaId"]),"valid catalog record");
   var cnRows=(JArray)JObject.Parse(chinaCatalog)["vehicles"];
   var cnSample=(JObject)JObject.Parse(sampleJson)["response"];
   var cnMatch=RangePresetService.Find(cnSample,rows,cnRows);
   Check(cnMatch.MinimumKm==420 && cnMatch.MaximumKm==439 && !cnMatch.Kilometers.HasValue,"2021 CN ambiguous pack interval");
   Check(cnMatch.Basis=="CN community","CN source label");
   var cn2022=(JObject)cnSample.DeepClone();
   cn2022["vin"]="LRW3E7FA0NC000001";
   Check(RangePresetService.Find(cn2022,rows,cnRows).Kilometers==439,"2022 CN classic RWD");
   cn2022["vehicle_config"]["charge_port_type"]="EU";
   Check(!RangePresetService.Find(cn2022,rows,cnRows).MinimumKm.HasValue,"Shanghai export excluded");
   cn2022["vehicle_config"]["charge_port_type"]="GB";
   cn2022["vehicle_config"]["wheel_type"]="Unknown18";
   Check(!RangePresetService.Find(cn2022,rows,cnRows).MinimumKm.HasValue,"unknown CN wheels excluded");
   var y=Vehicle("LRWYGCFD0PC000001","modely","50","Apollo19");
   y["vehicle_config"]["charge_port_type"]="GB";
   Check(RangePresetService.Find(y,rows,cnRows).Kilometers==435,"CN Y RWD");
   var lr=(JObject)y.DeepClone();lr["vehicle_config"]["trim_badging"]="74d";
   Check(RangePresetService.Find(lr,rows,cnRows).MinimumKm==520,"CN Y LR interval");
   var conflict=(JArray)cnRows.DeepClone();conflict.Add(cnRows[0].DeepClone());
   Check(!RangePresetService.Find(cnSample,rows,conflict).MinimumKm.HasValue,"ambiguous CN records excluded");
   foreach(JObject row in cnRows) Check((double)row["minKm"]>0 && (double)row["maxKm"]>=(double)row["minKm"] && ((string)row["source"]).StartsWith("https://"),"CN catalog data valid");
   return "PASS: reference matching, source, China isolation, unknown/missing configuration, ambiguous matches, VIN years, legacy badges, catalog validation, China single/interval references, export and wheel isolation.";
  }
 }
}
