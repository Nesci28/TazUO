// Browser-side Legion proof of concept. Pyodide executes the actual Python source in WASM;
// this bridge is the narrow surface that a production LegionAPI adapter will implement.
(function () {
    const output = () => document.getElementById("legion-output");
    const write = (message) => {
        const node = output();
        if (node) node.textContent += `${new Date().toISOString()} ${message}\n`;
    };

    let pyodidePromise;
    async function loadPython() {
        if (!pyodidePromise) {
            pyodidePromise = import("https://cdn.jsdelivr.net/pyodide/v0.27.2/full/pyodide.mjs")
                .then(({ loadPyodide }) => loadPyodide());
        }
        return pyodidePromise;
    }

    window.tazuoLegion = {
        async run() {
            const node = output();
            if (node) node.textContent = "Loading Python/WASM runtime…\n";
            try {
                const pyodide = await loadPython();
                const api = {
                    print: (message) => write(`LEGION: ${message}`),
                    pause: (seconds) => new Promise(resolve => setTimeout(resolve, seconds * 1000)),
                    process_callbacks: () => write("LEGION: callbacks processed")
                };
                pyodide.globals.set("API", api);
                await pyodide.runPythonAsync(`
import asyncio

async def legion_demo():
    API.print("script started")
    await API.pause(0.25)
    API.process_callbacks()
    API.print("script resumed after API.Pause")
    await API.pause(0.25)
    API.print("script completed")

await legion_demo()
`);
            } catch (error) {
                write(`ERROR: ${error}`);
                throw error;
            }
        }
    };
})();
