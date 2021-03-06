using System;
using System.Threading.Tasks;
using ServerShot.Framework.Core.Plugins.Alerts;

namespace Servershot.Framework.Entities.WebJob
{
    public abstract class PerpetualJibJob : JibJobModule, IPerpetualJibJob
    {
        private int _errorCount = 0;

        public async Task ProcessItem<T>(T item)
        {
            try
            {
                Log("Processing : " + item);
                await OnProcessItem(item);
                Processed(item);
                Log("Finished Processing : " + item);
                _errorCount = 0;
                ProcessedCount++;
            }
            catch (Exception eX)
            {
                Exception(eX);
                Log("EXCEPTION OCCURED in " + this.GetType().Name);
                Log("Exception details : " + eX.Message);
                Log("Exception Stacktrace : " + eX.StackTrace);
                Alert(new Alert()
                {
                    AlertLevel = AlertLevel.High,
                    Message = "Exception occured on webjob:" + this.GetType().Name + " : " + eX.Message
                });

                _errorCount++;

                if (_errorCount >= 3)
                {
                    Log("Module failed : " + this.GetType().Name);
                    Fail();
                }

                if (ThrowOnError)
                {
                    throw;
                }
            }
        }

        public abstract Task OnProcessItem<T>(T item);
    }
}