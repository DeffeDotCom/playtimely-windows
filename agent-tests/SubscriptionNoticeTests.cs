using PlaytimelyTray;
using Xunit;
public class SubscriptionNoticeTests {
 [Fact] public void NotifiesOnFreshExpiryOnlyAndPersistsAcrossRestarts(){var path=Path.Combine(Path.GetTempPath(),"pt-sub-"+Guid.NewGuid(),"receipt.json");try{var a=new SubscriptionNoticeReceipt(path);Assert.False(a.Observe("expired","device",false));Assert.True(a.Observe("expired","device",true));Assert.False(a.Observe("expired","device",true));var b=new SubscriptionNoticeReceipt(path);Assert.False(b.Observe("expired","device",true));Assert.False(b.Observe("active","device",true));Assert.True(b.Observe("expired","device",true));Assert.False(b.Observe("untrusted",null,true));}finally{Directory.Delete(Path.GetDirectoryName(path)!,true);}}
}
