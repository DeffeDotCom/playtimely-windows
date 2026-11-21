namespace GameControl;
// Candidates and backups are prepared before stopping the service. File replacement
// is atomic per executable; any failed replacement/start/health check restores both.
public static class UpgradeTransaction {
 public static void Run(string[] installed,string[] candidates,string backupDirectory,Action stop,Action startAndVerify){
  Directory.CreateDirectory(backupDirectory);
  var backups=installed.Select((_,i)=>Path.Combine(backupDirectory,i+".bak")).ToArray();
  for(int i=0;i<installed.Length;i++){if(!File.Exists(candidates[i]))throw new IOException("Missing staged binary");File.Copy(installed[i],backups[i],false);}
  var replaced=new bool[installed.Length];
  try{
   stop();
   for(int i=0;i<installed.Length;i++){File.Move(candidates[i],installed[i],true);replaced[i]=true;}
   startAndVerify();
  }catch(Exception updateFailure){
   // A locked untouched tray is already the old binary. Rewriting it during
   // recovery would repeat the lock error and prevent restarting the old service.
   try{stop();for(int i=0;i<installed.Length;i++)if(replaced[i])File.Copy(backups[i],installed[i],true);startAndVerify();}
   catch(Exception recoveryFailure){throw new AggregateException("Update and recovery failed; retained backup requires repair.",updateFailure,recoveryFailure);}
   throw new IOException("Update failed; previous version restored.",updateFailure);
  }
 }
}
