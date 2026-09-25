// Development tool only. See README.md for dependencies and invocation.
const { Resvg } = require('@resvg/resvg-js');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { execFileSync } = require('node:child_process');

for (const name of ['lock-keyhole', 'lock-keyhole-open', 'locate-fixed', 'plus', 'minus', 'route']) {
    const svg = readFileSync(join(__dirname, `${name}.svg`), 'utf8')
        .replace('stroke="currentColor"', 'stroke="#f5f5f5"');
    const png = new Resvg(svg, { fitTo: { mode: 'width', value: 72 } }).render().asPng();
    execFileSync('magick', [
        'png:-', '-resize', '18x18', '-depth', '8', '-strip',
        `PNG32:${join(__dirname, '..', `map-${name}.png`)}`
    ], { input: png });
}
