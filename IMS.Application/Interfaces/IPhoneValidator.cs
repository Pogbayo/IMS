namespace IMS.Application.Interfaces
{
    public interface IPhoneValidator
    {
        Task Validate(string number);
    }
}
