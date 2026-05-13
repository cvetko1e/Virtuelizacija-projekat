using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IWeatherService
    {
        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        TransferResponse StartSession(SessionMeta meta);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        TransferResponse PushSample(WeatherSample sample);

        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        TransferResponse EndSession();
    }
}
