import { ApplicationForm } from "../ui/ApplicationForm";

export default function ApplicationPage() {
  return (
    <section className="section page-shell">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Public başvuru</p>
          <h1>Başvuru formu</h1>
        </div>
        <div className="page-summary">
          Telefon doğrulaması ve KVKK onayları tamamlandıktan sonra başvuru referansı üretilir.
        </div>
      </div>
      <ApplicationForm />
    </section>
  );
}
