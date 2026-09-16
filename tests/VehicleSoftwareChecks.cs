using System;
using System.Threading.Tasks;
namespace TeslaMurphy.Services {
 internal static class TeslaConfiguration { public static string CommandBaseUrl => "https://proxy.test"; }
 public static class SoftwareChecks {
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
  static void Queue(int status,string body){FakeDriverHandler.Responses.Enqueue(new System.Net.Http.HttpResponseMessage((System.Net.HttpStatusCode)status){Content=new System.Net.Http.StringContent(body)});}
  public static async Task<string> Run(){
   var service=new VehicleSoftwareService("VIN-A");
   var charge = Newtonsoft.Json.Linq.JObject.Parse("{\"battery_range\":210.73,\"battery_level\":92}");
   Check(Math.Abs(VehicleSoftwareService.EstimateFullRangeKm(charge).Value - 368.62724035) < 0.001,"sample full range");
   charge["battery_level"] = 0;
   Check(!VehicleSoftwareService.EstimateFullRangeKm(charge).HasValue,"zero SOC");
   charge["battery_level"] = 101;
   Check(!VehicleSoftwareService.EstimateFullRangeKm(charge).HasValue,"invalid SOC");
   Check(!VehicleSoftwareService.EstimateFullRangeKm(null).HasValue,"missing data");
   FakeDriverHandler.Reply="{\"response\":{\"vin\":\"VIN-A\",\"vehicle_state\":{\"car_version\":\"2026.1\",\"software_update\":{\"status\":\"available\"}}}}";
   var state=await service.RefreshAsync();
   Check((string)state["vehicle_state"]["car_version"]=="2026.1" && FakeDriverHandler.Method=="GET","read vehicle state");
   FakeDriverHandler.Reply="{\"response\":{\"vin\":\"VIN-B\",\"vehicle_state\":{}}}";
   try{await service.RefreshAsync();throw new Exception("wrong VIN accepted");}catch(InvalidOperationException){}
   FakeDriverHandler.Reply="{\"response\":{\"result\":true}}";
   await service.SendAsync(false);
   Check(FakeDriverHandler.Path.EndsWith("/schedule_software_update") && FakeDriverHandler.Body=="{\"offset_sec\":0}","install command");
   await service.SendAsync(true);
   Check(FakeDriverHandler.Path.EndsWith("/cancel_software_update") && FakeDriverHandler.Body=="{}","cancel command");
   FakeDriverHandler.Reply="{\"response\":{\"result\":false}}";
   try{await service.SendAsync(false);throw new Exception("false accepted");}catch(InvalidOperationException){}
   FakeDriverHandler.Status=500;FakeDriverHandler.Calls=0;
   try{await service.SendAsync(false);throw new Exception("500 accepted");}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Calls==1,"no ambiguous install replay");
   string good="{\"response\":{\"vin\":\"VIN-A\",\"vehicle_state\":{}}}";
   string awake="{\"response\":{\"vin\":\"VIN-A\",\"state\":\"online\"}}";
   FakeDriverHandler.Status=200;FakeDriverHandler.Requests.Clear();
   Queue(401,"{}");Queue(401,"{}");Queue(200,awake);Queue(200,good);
   await service.RefreshAsync();
   Check(FakeDriverHandler.Requests.Count==4 && FakeDriverHandler.Requests[2]=="POST https://example.test/api/1/vehicles/VIN-A/wake_up" && FakeDriverHandler.Requests[3].StartsWith("GET "),"401 wakes via regional API then checks once");
   FakeDriverHandler.Requests.Clear();
   Queue(401,"{}");Queue(200,good);
   await service.RefreshAsync();
   Check(FakeDriverHandler.Requests.Count==2 && FakeDriverHandler.Requests.TrueForAll(r=>r.StartsWith("GET ")),"successful token refresh avoids wake");
   FakeDriverHandler.Requests.Clear();
   Queue(401,"{}");Queue(401,"{}");Queue(200,awake);Queue(401,"{}");Queue(401,"{}");
   try{await service.RefreshAsync();throw new Exception("persistent 401 accepted");}catch(InvalidOperationException ex){Check(ex.Message.Contains("Still unauthorized"),"persistent 401 error");}
   Check(FakeDriverHandler.Requests.FindAll(r=>r.Contains("/wake_up")).Count==1 && FakeDriverHandler.Responses.Count==0,"bounded wake retry");
   FakeDriverHandler.Requests.Clear();
   Queue(401,"{}");Queue(401,"{}");Queue(403,"{}");
   try{await service.RefreshAsync();throw new Exception("wake failure accepted");}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Requests.Count==3,"wake failure stops recovery");
   FakeDriverHandler.Requests.Clear();
   Queue(500,"{}");
   try{await service.RefreshAsync();throw new Exception("500 read accepted");}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Requests.Count==1,"other status does not wake");
   Models.AppSettings.Instance.IsTestMode=true;FakeDriverHandler.Calls=0;
   try{await service.SendAsync(false);throw new Exception("demo accepted");}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Calls==0,"demo isolation");
   return "PASS: state parsing, VIN isolation, install and cancel payloads, rejected result, no 500 replay, demo isolation, 401 auth refresh, bounded wake recovery and wake failure.";
  }
 }
}
