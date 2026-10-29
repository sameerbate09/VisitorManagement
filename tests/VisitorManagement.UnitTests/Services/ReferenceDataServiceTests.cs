using FluentAssertions;
using NSubstitute;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Services;

namespace VisitorManagement.UnitTests.Services;

public class ReferenceDataServiceTests
{
    private readonly IReferenceDataRepository _repository = Substitute.For<IReferenceDataRepository>();
    private readonly ReferenceDataService _sut;

    public ReferenceDataServiceTests()
    {
        _repository.SearchHostsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<HostDto>)new List<HostDto>());
        _sut = new ReferenceDataService(_repository);
    }

    [Theory]
    [InlineData(null, 10)]    // default
    [InlineData(0, 1)]        // lower clamp
    [InlineData(5, 5)]
    [InlineData(999, 25)]     // upper clamp
    public async Task SearchHostsAsync_clamps_the_result_limit(int? requested, int expected)
    {
        await _sut.SearchHostsAsync("an", requested, CancellationToken.None);

        await _repository.Received(1).SearchHostsAsync("an", expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchHostsAsync_trims_the_term_and_treats_blank_as_no_filter()
    {
        await _sut.SearchHostsAsync("  an  ", null, CancellationToken.None);
        await _sut.SearchHostsAsync("   ", null, CancellationToken.None);

        await _repository.Received(1).SearchHostsAsync("an", 10, Arg.Any<CancellationToken>());
        await _repository.Received(1).SearchHostsAsync(null, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchHostsAsync_caps_very_long_terms_at_50_characters()
    {
        await _sut.SearchHostsAsync(new string('a', 80), null, CancellationToken.None);

        await _repository.Received(1).SearchHostsAsync(
            Arg.Is<string?>(t => t != null && t.Length == 50), 10, Arg.Any<CancellationToken>());
    }
}