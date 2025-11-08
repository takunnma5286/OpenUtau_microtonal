using Xunit;
using Xunit.Abstractions;

namespace OpenUtau.Core.Util {
    public class MusicMathTest {
        readonly ITestOutputHelper output;

        public MusicMathTest(ITestOutputHelper output) {
            this.output = output;
        }

        [Fact]
        public void ToneNameTest() {
            for (int i = 24; i < 108; ++i) {
                string name = MusicMath.GetToneName(i, 12);
                int tone = MusicMath.NameToTone(name, 12);
                output.WriteLine($"{i} -> {name} -> {tone}");
                Assert.Equal(i, tone);
            }
        }
    }
}
