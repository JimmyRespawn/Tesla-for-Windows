using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace TeslaMurphy.Models {
 public class AppSettings {
  public static AppSettings Instance=new AppSettings();
  public string Base_URL="https://example.test", Access_token="old";
  public bool IsTestMode;
 }
}
namespace TeslaMurphy.Services {
 internal static class TeslaFleetServices {
  public static int Refreshes;
  public static Task<bool> EnsureSessionAsync(string rejectedToken=null,CancellationToken cancellationToken=default(CancellationToken)) {
   if(rejectedToken!=null){Refreshes++;Models.AppSettings.Instance.Access_token="new";}
   return Task.FromResult(true);
  }
 }
 public class FakeDriverHandler:HttpMessageHandler {
  public static string Method,Path,Body,Reply="{\"response\":true}";
  public static int Status=200, Calls;
  public static bool First401;
  public static readonly System.Collections.Generic.Queue<HttpResponseMessage> Responses = new System.Collections.Generic.Queue<HttpResponseMessage>();
  public static readonly System.Collections.Generic.List<string> Requests = new System.Collections.Generic.List<string>();
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c) {
   Calls++;Method=r.Method.Method;Path=r.RequestUri.PathAndQuery;
   Body=r.Content==null?null:await r.Content.ReadAsStringAsync();
   Requests.Add(r.Method.Method + " " + r.RequestUri.AbsoluteUri);
   if(Responses.Count>0)return Responses.Dequeue();
   return new HttpResponseMessage((HttpStatusCode)(First401 && Calls==1?401:Status)) { Content=new StringContent(Reply) };
  }
 }
 public static class DriverChecks {
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
  public static async Task<string> Run() {
   var service=new DriverManagementService("VIN-A");
   await service.Remove("123/456");
   Check(FakeDriverHandler.Method=="DELETE" && FakeDriverHandler.Path.EndsWith("?share_user_id=123%2F456") && FakeDriverHandler.Body==null,"DELETE query");
   await service.Create();
   Check(FakeDriverHandler.Method=="POST" && FakeDriverHandler.Path.EndsWith("/invitations") && JObject.Parse(FakeDriverHandler.Body).Count==0,"create body");
   await service.Invitations(2);
   Check(FakeDriverHandler.Method=="GET" && FakeDriverHandler.Path.EndsWith("?page=2&per_page=25"),"pagination");
   await service.Revoke("100");
   Check(FakeDriverHandler.Path.EndsWith("/invitations/100/revoke"),"revoke");
   await service.Redeem("https://www.tesla.cn/_rs/1/abcdefghijklmnop");
   Check((string)JObject.Parse(FakeDriverHandler.Body)["code"]=="abcdefghijklmnop","redeem code");
   try{DriverManagementService.InvitationCode("https://evil.test/_rs/1/abcdefghijklmnop");throw new Exception("untrusted accepted");}
   catch(ArgumentException){}
   FakeDriverHandler.Status=403;
   try{await service.Create();throw new Exception("403 accepted");}catch(InvalidOperationException){}
   FakeDriverHandler.Status=200;FakeDriverHandler.Reply="{\"response\":false}";
   try{await service.Create();throw new Exception("false accepted");}catch(InvalidOperationException){}
   FakeDriverHandler.Reply="{\"response\":\"ok\"}";
   await service.Remove("123");
   FakeDriverHandler.First401=true;FakeDriverHandler.Calls=0;
   await service.Create();
   Check(FakeDriverHandler.Calls==2 && TeslaFleetServices.Refreshes==1,"401 retry once");
   FakeDriverHandler.First401=false;FakeDriverHandler.Status=500;FakeDriverHandler.Calls=0;
   try{await service.Create();}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Calls==1,"no mutation retry on 500");
   Models.AppSettings.Instance.IsTestMode=true;FakeDriverHandler.Calls=0;
   try{await service.Create();}catch(InvalidOperationException){}
   Check(FakeDriverHandler.Calls==0,"demo blocked");
   return "PASS: DELETE query, JSON body, pagination, revoke, redeem, URL validation, 403, false result, 401 retry, no 500 replay, demo isolation.";
  }
 }
}
