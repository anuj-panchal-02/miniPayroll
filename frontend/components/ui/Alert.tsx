type AlertTone = "error" | "success" | "status";

type AlertProps = {
  tone?: AlertTone;
  children?: string | null;
  className?: string;
};

export function Alert({ tone = "error", children, className }: AlertProps) {
  if (!children) {
    return null;
  }

  const role = tone === "status" ? "status" : tone === "success" ? "status" : "alert";
  return (
    <p
      className={["mp-alert", `mp-alert--${tone}`, className].filter(Boolean).join(" ")}
      role={role}
    >
      {children}
    </p>
  );
}
