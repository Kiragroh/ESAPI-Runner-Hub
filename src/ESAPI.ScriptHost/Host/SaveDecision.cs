using EsapiRunnerHub.Configuration;

namespace EsapiScriptHost.Host
{
    public enum SaveChoice
    {
        Discard,
        Save
    }

    public enum SaveAction
    {
        CloseWithoutSave,
        SaveOnce
    }

    public static class SaveDecision
    {
        public static SaveAction Decide(bool completedNormally, WriteMode writeMode, SaveChoice choice)
        {
            return completedNormally && writeMode == WriteMode.ConfirmSave && choice == SaveChoice.Save
                ? SaveAction.SaveOnce
                : SaveAction.CloseWithoutSave;
        }
    }
}
