import { ApplicationForm } from "../ui/ApplicationForm";

export default function ApplicationPage() {
  return (
    <section className="section">
      <h1>Başvuru Formu</h1>
      <p className="lead">
        Telefon doğrulaması ve KVKK onayları tamamlanmadan başvuru oluşturulmaz. Demo ortamında SMS kodu ekranda gösterilir.
      </p>
      <ApplicationForm />
    </section>
  );
}
