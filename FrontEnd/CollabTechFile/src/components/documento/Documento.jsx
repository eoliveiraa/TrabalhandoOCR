import "./Documento.css";
import { useState } from "react";

export default function Documento({ pdfFile, ocrData }) {
  const [mostrarTexto, setMostrarTexto] = useState(false);

  // O texto extraído do OCR pode estar em ocrData.content
  const textoExtraido =
    ocrData?.content ||
    (ocrData?.documents?.[0]?.content ?? "Nenhum texto reconhecido.");

  return (
    <div className="documento-container">
      <div className="visualizacao">
        <iframe
          src={pdfFile}
          type="application/pdf"
          title="Documento PDF"
          width="100%"
          height="600px"
        ></iframe>
      </div>

      <div className="acoes">
        <button
          className="btn-toggle-texto"
          onClick={() => setMostrarTexto(!mostrarTexto)}
        >
          {mostrarTexto ? "Ocultar texto OCR" : "Mostrar texto OCR"}
        </button>
      </div>

      {mostrarTexto && (
        <div className="painel-ocr">
          <h3>Texto reconhecido pela IA:</h3>
          <pre className="texto-ocr">
            {textoExtraido.length > 0
              ? textoExtraido
              : "Nenhum conteúdo detectado no documento."}
          </pre>
        </div>
      )}
    </div>
  );
}
