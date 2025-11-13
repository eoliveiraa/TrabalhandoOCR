// src/pages/UploadDoc/UploadDoc.jsx
import { useState } from "react";
import axios from "axios";
import "./UploadOCR.css";

export default function UploadDoc() {
  const [file, setFile] = useState(null);
  const [texto, setTexto] = useState("");
  const [loading, setLoading] = useState(false);

  const handleUpload = async () => {
    if (!file) return alert("Selecione um arquivo primeiro!");
    setLoading(true);

    try {
      const formData = new FormData();
      formData.append("arquivo", file);

      const res = await axios.post("https://localhost:7147/api/ocr/analisar", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      setTexto(res.data.texto);
    } catch (err) {
      console.error(err);
      alert("Erro ao processar documento. Verifique a API e a chave da Azure.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="upload-container">
      <h2>Analisar Documento com IA</h2>
      <input
        type="file"
        accept=".pdf,.png,.jpg"
        onChange={(e) => setFile(e.target.files[0])}
      />
      <button onClick={handleUpload} disabled={loading}>
        {loading ? "Analisando..." : "Enviar"}
      </button>

      {texto && (
        <div className="resultado">
          <h3>Texto extraído:</h3>
          <pre>{texto}</pre>
        </div>
      )}
    </div>
  );
}
