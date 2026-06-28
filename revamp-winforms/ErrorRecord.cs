using System;
using System.Text.Json.Serialization;

namespace EazyRentRevamp
{
    public class ErrorRecord
    {
        [JsonPropertyName("Room_no")]
        public string RoomNo { get; set; } = string.Empty;

        [JsonPropertyName("Descr1")]
        public string Descr1 { get; set; } = string.Empty;

        [JsonPropertyName("Rent_no")]
        public string RentNo { get; set; } = string.Empty;

        [JsonPropertyName("Date_OF_DB")]
        public string DateOfDb { get; set; } = string.Empty;

        [JsonPropertyName("Amount")]
        public string Amount { get; set; } = string.Empty;

        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("DebitAccount1")]
        public string DebitAccount1 { get; set; } = string.Empty;

        [JsonPropertyName("DebitAccount2")]
        public string DebitAccount2 { get; set; } = string.Empty;

        [JsonPropertyName("CreditAccount1")]
        public string CreditAccount1 { get; set; } = string.Empty;

        [JsonPropertyName("CreditAccount2")]
        public string CreditAccount2 { get; set; } = string.Empty;

        [JsonPropertyName("DrcostCenter")]
        public string DrCostCenter { get; set; } = string.Empty;

        [JsonPropertyName("CrcostCenter")]
        public string CrCostCenter { get; set; } = string.Empty;

        [JsonPropertyName("Ser")]
        public string Ser { get; set; } = string.Empty;

        [JsonPropertyName("ERORR")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("failed_requests_timestamp")]
        public string FailedRequestsTimestamp { get; set; } = string.Empty;
    }
}

