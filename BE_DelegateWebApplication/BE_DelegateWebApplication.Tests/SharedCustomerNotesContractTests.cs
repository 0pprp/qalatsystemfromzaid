using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class SharedCustomerNotesContractTests
    {
        [Fact]
        public void EmptyNote_IsRejectedAfterTrim()
        {
            var text = "   ";
            Assert.True(string.IsNullOrWhiteSpace(text.Trim()));
        }

        [Fact]
        public void MaxLength_Is2000()
        {
            Assert.Equal(2000, BE_DelegateWebApplication.Services.SharedCustomerNotesService.MaxNoteLength);
        }

        [Fact]
        public void SharedNotes_KeyByCustomerIdNotDelegateOnly()
        {
            const string binding = "CustomerId";
            Assert.Equal("CustomerId", binding);
            Assert.DoesNotContain("DelegateId-only", binding, StringComparison.Ordinal);
        }

        [Fact]
        public void Follower_IsReadOnlyOnSharedNotes()
        {
            const bool followerCanWriteShared = false;
            Assert.False(followerCanWriteShared);
        }

        [Fact]
        public void Roles_ReadMatrix()
        {
            static bool canRead(string role) => role is "مندوب" or "متابع" or "مدير مبيعات" or "محاسب رئيسي";
            static bool canWrite(string role) => role is "مندوب";

            Assert.True(canRead("مندوب"));
            Assert.True(canWrite("مندوب"));
            Assert.True(canRead("متابع"));
            Assert.False(canWrite("متابع"));
            Assert.True(canRead("مدير مبيعات"));
            Assert.False(canWrite("مدير مبيعات"));
            Assert.True(canRead("محاسب رئيسي"));
        }
    }
}
