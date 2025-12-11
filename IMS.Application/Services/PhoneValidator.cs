using IMS.Application.Interfaces;
using PhoneNumbers;
using System.ComponentModel.DataAnnotations;

namespace IMS.Application.Services
{
    public class PhoneValidator : IPhoneValidator
    {
        public Task Validate(string number)
        {
            var phoneUtil = PhoneNumberUtil.GetInstance();
            try
            {
                var parsed = phoneUtil.Parse(number, null);
                if (!phoneUtil.IsValidNumber(parsed))
                    throw new ValidationException("Invalid phone number.");
            }
            catch
            {
                throw new ValidationException("Invalid phone number.");
            }
            return Task.CompletedTask; 

        }
    }
}
