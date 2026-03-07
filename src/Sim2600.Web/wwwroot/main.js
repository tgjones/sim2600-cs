import { dotnet } from './_framework/dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

setModuleImports('main.js', {
    dom: {
        setSimState: (halfCycles, vsync, vblank, shouldRestartImage, hasPixel, color) => {
            document.getElementById('half-cycles').textContent = halfCycles;
            document.getElementById('vsync-led').className  = 'indicator' + (vsync  ? ' active' : '');
            document.getElementById('vblank-led').className = 'indicator' + (vblank ? ' active' : '');

            if (shouldRestartImage) {
                restartImage();
            }

            if (hasPixel) {
                updateImage(color);
            }
        }
    }
});

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

const dropZone = document.getElementById('drop-zone');
const romInput = document.getElementById('rom-input');
const screen   = document.getElementById('screen');
const ctx      = screen.getContext('2d');

const SCREEN_WIDTH  = 228;
const SCREEN_HEIGHT = 262;

const imageData = ctx.createImageData(SCREEN_WIDTH, SCREEN_HEIGHT);

let pixelX = 0;
let pixelY = 0;

dropZone.addEventListener('click', () => romInput.click());

dropZone.addEventListener('dragover', e => {
    e.preventDefault();
    dropZone.classList.add('drag-over');
});

dropZone.addEventListener('dragleave', () => dropZone.classList.remove('drag-over'));

dropZone.addEventListener('drop', e => {
    e.preventDefault();
    dropZone.classList.remove('drag-over');
    const file = e.dataTransfer.files[0];
    if (file) loadRom(file);
});

romInput.addEventListener('change', () => {
    if (romInput.files[0]) loadRom(romInput.files[0]);
});

function loadRom(file) {
    const reader = new FileReader();
    reader.onload = e => {
        const rom = new Uint8Array(e.target.result);
        dropZone.style.display = 'none';
        screen.style.display = 'block';
        exports.Sim2600WebProgram.StartSimulator(rom);
        requestAnimationFrame(runHalfCycle);
    };
    reader.readAsArrayBuffer(file);
}

function runHalfCycle() {
    // TODO: batch
    exports.Sim2600WebProgram.RunHalfCycle();

    requestAnimationFrame(runHalfCycle);
}

function restartImage() {
    pixelX = 0;
    pixelY = 0;
}

function updateImage(color) {
    const i = (pixelY * SCREEN_WIDTH + pixelX) * 4;
    imageData.data[i    ] = (color >>> 24) & 0xFF;
    imageData.data[i + 1] = (color >>> 16) & 0xFF;
    imageData.data[i + 2] = (color >>>  8) & 0xFF;
    imageData.data[i + 3] = (color       ) & 0xFF;
    ctx.putImageData(imageData, 0, 0, pixelX, pixelY, 1, 1);

    pixelX++;
    if (pixelX >= SCREEN_WIDTH) {
        pixelX = 0;
        pixelY++;
        if (pixelY >= SCREEN_HEIGHT) {
            pixelY = 0;
        }
    }
}