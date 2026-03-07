import { dotnet } from './_framework/dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

setModuleImports('main.js', {
    dom: {
        setInnerText: (selector, time) => document.querySelector(selector).innerText = time
    }
});

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

const dropZone = document.getElementById('drop-zone');
const romInput = document.getElementById('rom-input');
const screen   = document.getElementById('screen');

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
    };
    reader.readAsArrayBuffer(file);
}

// Status bar updater — called from .NET via JS interop
window.setSimState = (halfCycles, vsync, vblank, restartImage, color) => {
    document.getElementById('half-cycles').textContent = halfCycles;
    document.getElementById('vsync-led').className  = 'indicator' + (vsync  ? ' active' : '');
    document.getElementById('vblank-led').className = 'indicator' + (vblank ? ' active' : '');
};