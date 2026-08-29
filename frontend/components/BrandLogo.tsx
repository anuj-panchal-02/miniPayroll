import "./BrandLogo.css";

type BrandLogoProps = {
  size?: "nav" | "auth";
};

export function BrandLogo({ size = "nav" }: BrandLogoProps) {
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src="/logo.png"
      alt="miniPayroll"
      width={491}
      height={193}
      className={`brand-logo brand-logo--${size}`}
    />
  );
}
