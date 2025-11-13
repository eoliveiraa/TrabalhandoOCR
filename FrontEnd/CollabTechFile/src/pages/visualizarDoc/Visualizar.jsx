import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import api from "../../services/Service";
import Documento from "../../components/documento/Documento";
import MenuLateral from "../../components/menuLateral/MenuLateral";
import Cabecalho from "../../components/cabecalho/Cabecalho";

export default function VisualizarDoc() {
  const { id } = useParams();
  const [ocrData, setOcrData] = useState(null);
  const [pdfUrl, setPdfUrl] = useState(null);
  const [erro, setErro] = useState(null);
  const [textoOCR, setTextoOCR] = useState(""); 
  const [loadingOCR, setLoadingOCR] = useState(false); 
  const [modalAberta, setModalAberta] = useState(false);

  useEffect(() => {
    async function buscarOCR() {
      try {
        const respostaOCR = await api.get(`/documentos/ocr-detalhado/${id}`);
        setOcrData(respostaOCR.data);

        const respostaPDF = await api.get(`/documentos/${id}/arquivo`, {
          responseType: "blob",
        });
        setPdfUrl(URL.createObjectURL(respostaPDF.data));
      } catch (erro) {
        console.error("Erro ao buscar documento OCR:", erro);
        setErro(
          "Falha ao carregar o documento. Verifique se o servidor está ativo e o ID é válido."
        );
      }
    }

    buscarOCR();
  }, [id]);

  const handleExtrairOCR = async () => {
    if (!id) return;
    setLoadingOCR(true);
    try {
      const res = await api.post(`/ocr/analisar/documento/${id}`);
      setTextoOCR(res.data.texto);
      setModalAberta(true);
    } catch (err) {
      console.error("Erro ao processar OCR:", err);
      alert("Falha ao processar o documento via OCR.");
    } finally {
      setLoadingOCR(false);
    }
  };

  if (erro) {
    return (
      <div className="containerGeral">
        <MenuLateral />
        <main className="conteudoPrincipal">
          <Cabecalho />
          <h1>Erro ao carregar documento</h1>
          <p style={{ color: "red" }}>{erro}</p>
        </main>
      </div>
    );
  }

  return (
    <div className="containerGeral">
      <MenuLateral />
      <main className="conteudoPrincipal">
        <Cabecalho />
        <h1>Visualização de Documento</h1>

        {ocrData && pdfUrl ? (
          <div>
            <Documento pdfFile={pdfUrl} ocrData={ocrData} />

            <div
              style={{
                background: "#f7f7f7",
                marginTop: 20,
                padding: 15,
                borderRadius: 10,
              }}
            >
              <h3>Resumo do OCR existente:</h3>
              <pre
                style={{ maxHeight: 300, overflowY: "auto", textAlign: "left" }}
              >
                {ocrData?.content
                  ? ocrData.content.substring(0, 1000) + "..."
                  : JSON.stringify(ocrData, null, 2)}
              </pre>
            </div>

            {/* Botão para OCR em modal */}
            <button
              onClick={handleExtrairOCR}
              disabled={loadingOCR}
              style={{ marginTop: 20 }}
            >
              {loadingOCR ? "Processando OCR..." : "Abrir OCR em modal"}
            </button>

            {/* Modal OCR */}
            {modalAberta && (
              <div
                style={{
                  position: "fixed",
                  top: 0,
                  left: 0,
                  width: "100vw",
                  height: "100vh",
                  background: "rgba(0,0,0,0.5)",
                  display: "flex",
                  justifyContent: "center",
                  alignItems: "center",
                  zIndex: 1000,
                }}
              >
                <div
                  style={{
                    background: "#fff",
                    padding: 20,
                    borderRadius: 10,
                    maxWidth: "90%",
                    maxHeight: "90%",
                    overflowY: "auto",
                  }}
                >
                  <h3>Texto extraído pelo OCR</h3>
                  <pre style={{ whiteSpace: "pre-wrap" }}>{textoOCR}</pre>
                  <button
                    onClick={() => setModalAberta(false)}
                    style={{ marginTop: 10 }}
                  >
                    Fechar
                  </button>
                </div>
              </div>
            )}
          </div>
        ) : (
          <p>Carregando documento...</p>
        )}
      </main>
    </div>
  );
}
