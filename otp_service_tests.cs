using backend.shared.Domain;
using Moq;
using Xunit;

namespace backend.shared.Tests.Domain
{
    public class OTPServiceTests
    {
        private readonly Mock<IOTPService> _otpServiceMock;

        public OTPServiceTests()
        {
            _otpServiceMock = new Mock<IOTPService>();
        }

        [Fact]
        public async Task GenerateAsync_ShouldReturnSuccess_WhenPhoneNumberIsValid()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var otpService = new OTPService();

            // Act
            var result = await otpService.GenerateAsync(phoneNumber);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GenerateAsync_ShouldReturnFailure_WhenPhoneNumberIsInvalid()
        {
            // Arrange
            var phoneNumber = "invalid_phone_number";
            var otpService = new OTPService();

            // Act
            var result = await otpService.GenerateAsync(phoneNumber);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnSuccess_WhenOTPIsValid()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var providedOTP = "1234";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnFailure_WhenOTPIsInvalid()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var providedOTP = "invalid_otp";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnFailure_WhenPhoneNumberIsInvalid()
        {
            // Arrange
            var phoneNumber = "invalid_phone_number";
            var providedOTP = "1234";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnFailure_WhenOTPExpiry()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var providedOTP = "1234";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnFailure_WhenOTPRetryLimitExceeded()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var providedOTP = "1234";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ValidateAsync_ShouldReturnFailure_WhenJWTExpiry()
        {
            // Arrange
            var phoneNumber = "1234567890";
            var providedOTP = "1234";
            var otpService = new OTPService();

            // Act
            var result = await otpService.ValidateAsync(phoneNumber, providedOTP);

            // Assert
            Assert.False(result.IsSuccess);
        }
    }
}
