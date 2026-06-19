namespace MiniBank.Shared.Domain.Enums;

public enum DeclineReason { None = 0, InsufficientFunds = 1, CardNotActive = 2, CardExpired = 3, LimitExceeded = 4, FraudSuspected = 5, InvalidMerchant = 6, InvalidPin = 7, SystemError = 8 }
