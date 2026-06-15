import os
data = open('deps/noto_sans/otf/SubsetOTF/JP/NotoSansJP-Regular.otf', 'rb').read()
with open('deps/noto_sans/noto_sans.hpp', 'w') as f:
    f.write('const unsigned char notoSansJpRegular[] = {')
    f.write(','.join(hex(b) for b in data))
    f.write('};\n')
    f.write(f'const unsigned int notoSansJpRegularLen = {len(data)};\n')
