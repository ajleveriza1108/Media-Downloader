let tab=null;
const status=t=>document.getElementById("status").textContent=t||"";
async function send(mode){
  if(!tab?.url){status("No supported active tab.");return;}
  status("Sending to MediaDock…");
  const r=await chrome.runtime.sendMessage({type:"md-send",item:{url:tab.url,title:tab.title||"",source:"popup"},mode,kind:"page"});
  status(r?.ok ? (mode==="analyze"?"Opened in MediaDock.":"Sent to MediaDock.") : (r?.error||"MediaDock handler failed."));
}
document.getElementById("send").onclick=()=>send("download");
document.getElementById("analyze").onclick=()=>send("analyze");
document.getElementById("auto").onchange=async e=>{
  await chrome.runtime.sendMessage({type:"md-set-auto-intercept",value:e.target.checked});
  status(e.target.checked?"Automatic interception enabled.":"Automatic interception disabled.");
};
(async()=>{
  [tab]=await chrome.tabs.query({active:true,currentWindow:true});
  const s=await chrome.runtime.sendMessage({type:"md-get-settings"});
  document.getElementById("auto").checked=s?.autoIntercept!==false;
})();