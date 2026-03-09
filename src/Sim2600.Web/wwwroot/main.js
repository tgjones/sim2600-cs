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
                simFrameStartTime = performance.now();
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

const SCREEN_WIDTH  = 228;
const SCREEN_HEIGHT = 262;

const dropZone = document.getElementById('drop-zone');
const romInput = document.getElementById('rom-input');
const screen   = document.getElementById('screen');
const ctx      = screen.getContext('2d');

screen.width  = SCREEN_WIDTH;
screen.height = SCREEN_HEIGHT;

const SCREEN_RATIO = SCREEN_WIDTH / SCREEN_HEIGHT;
new ResizeObserver(entries => {
    const { width, height } = entries[0].contentRect;
    if (width / height > SCREEN_RATIO) {
        // height-constrained: fit to height
        screen.style.height = `${height}px`;
        screen.style.width  = `${height * SCREEN_RATIO}px`;
    } else {
        // width-constrained: fit to width
        screen.style.width  = `${width}px`;
        screen.style.height = `${width / SCREEN_RATIO}px`;
    }
}).observe(document.querySelector('main'));

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

const TARGET_BUDGET = 0.80; // fraction of frame time the tuner aims for
const HARD_CAP      = 0.87; // absolute max — loop breaks early if exceeded
let callsPerFrame = 1;
let lastFrameTime = null;

// Frame time tracking (resets at shouldRestartImage, finishes at next shouldRestartImage)
let simFrameStartTime = null;
const lastFrameTimeEl    = document.getElementById('last-frame-time');
const msPerHalfCycleEl   = document.getElementById('ms-per-half-cycle');

// Running average: ms per half cycle, updated once per second
// simExecMs accumulates only the time spent inside the simulation loop, excluding browser overhead
let halfCycleCount = 0;
let simExecMs      = 0;
setInterval(() => {
    if (halfCycleCount > 0) {
        msPerHalfCycleEl.textContent = `${(simExecMs / halfCycleCount).toFixed(1)} ms / half cycle`;
    }
    halfCycleCount = 0;
    simExecMs      = 0;
}, 1000);

function runHalfCycle(timestamp) {
    const frameDuration = lastFrameTime !== null ? timestamp - lastFrameTime : 16.67;
    lastFrameTime = timestamp;

    const budget  = frameDuration * TARGET_BUDGET;
    const hardCap = frameDuration * HARD_CAP;
    const t0 = performance.now();

    for (let i = 0; i < callsPerFrame; i++) {
        exports.Sim2600WebProgram.RunHalfCycle();
        halfCycleCount++;
        if (performance.now() - t0 >= hardCap) break;
    }

    const elapsed = performance.now() - t0;
    simExecMs += elapsed;

    // Adjust calls per frame: scale by budget/elapsed, clamped to avoid wild swings
    if (elapsed > 0) {
        const ideal = callsPerFrame * (budget / elapsed);
        callsPerFrame = Math.max(1, Math.round(callsPerFrame * 0.75 + ideal * 0.25));
    }

    if (simFrameStartTime !== null) {
        lastFrameTimeEl.textContent = `Current frame time: ${((performance.now() - simFrameStartTime) / 1000).toFixed(1)} seconds`;
    }

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