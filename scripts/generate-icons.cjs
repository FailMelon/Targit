// Render the supplied vector without changing its artwork. Pass the sharp module path if needed.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.argv[2] || 'sharp');
const assets = path.resolve(__dirname, '../src/GitLlama.Desktop/Assets');
(async () => {
  const source = fs.readFileSync(path.join(assets, 'gitllama.svg'));
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const images = await Promise.all(sizes.map(size => sharp(source).resize(size, size).png().toBuffer()));
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  sizes.forEach((size, i) => {
    const entry = 6 + i * 16;
    header[entry] = header[entry + 1] = size === 256 ? 0 : size;
    header.writeUInt16LE(1, entry + 4); header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(images[i].length, entry + 8); header.writeUInt32LE(offset, entry + 12);
    offset += images[i].length;
  });
  fs.writeFileSync(path.join(assets, 'gitllama.ico'), Buffer.concat([header, ...images]));
  await sharp(source).resize(512, 512).png().toFile(path.join(assets, 'gitllama.png'));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
